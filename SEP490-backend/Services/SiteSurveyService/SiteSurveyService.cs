using Microsoft.EntityFrameworkCore;
using Sep490_Backend.Controllers;
using Sep490_Backend.DTO;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.SiteSurvey;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Infra.Helps;
using Sep490_Backend.Services.CacheService;
using Sep490_Backend.Services.DataService;
using Sep490_Backend.Services.HelperService;
using Sep490_Backend.Services.GoogleDriveService;
using System.Text.Json;

namespace Sep490_Backend.Services.SiteSurveyService
{
    public interface ISiteSurveyService
    {
        Task<SiteSurvey> SaveSiteSurvey(SaveSiteSurveyDTO model, int actionBy);
        Task<int> DeleteSiteSurvey(int id, int actionBy);
        Task<SiteSurvey> GetSiteSurveyDetail(int projectId, int actionBy);
    }

    public class SiteSurveyService : ISiteSurveyService
    {
        private readonly BackendContext _context;
        private readonly ICacheService _cacheService;
        private readonly IHelperService _helpService;
        private readonly IDataService _dataService;
        private readonly IGoogleDriveService _googleDriveService;

        // Định nghĩa các key cache cho SiteSurvey
        private const string SITE_SURVEY_BY_PROJECT_CACHE_KEY = "SITE_SURVEY:PROJECT:{0}"; // Pattern: SITE_SURVEY:PROJECT:projectId
        private const string SITE_SURVEY_BY_USER_CACHE_KEY = "SITE_SURVEY:USER:{0}"; // Pattern: SITE_SURVEY:USER:userId

        public SiteSurveyService(
            BackendContext context, 
            IDataService dataService, 
            ICacheService cacheService, 
            IHelperService helpService,
            IGoogleDriveService googleDriveService)
        {
            _context = context;
            _cacheService = cacheService;
            _helpService = helpService;
            _dataService = dataService;
            _googleDriveService = googleDriveService;
        }

        public async Task<int> DeleteSiteSurvey(int id, int actionBy)
        {
            // Kiểm tra vai trò người dùng (Technical Manager hoặc Executive Board)
            bool isExecutiveBoard = _helpService.IsInRole(actionBy, RoleConstValue.EXECUTIVE_BOARD);
            bool isTechnicalManager = _helpService.IsInRole(actionBy, RoleConstValue.TECHNICAL_MANAGER);
            
            if (!isExecutiveBoard && !isTechnicalManager)
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Tìm SiteSurvey cần xóa
            var data = await _context.SiteSurveys.FirstOrDefaultAsync(t => t.Id == id && !t.Deleted);
            if (data == null)
            {
                throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
            }
            
            // Nếu là Technical Manager, kiểm tra có thuộc project không
            if (isTechnicalManager && !isExecutiveBoard)
            {
                var isProjectMember = await _context.ProjectUsers
                    .AnyAsync(pu => pu.ProjectId == data.ProjectId && pu.UserId == actionBy && !pu.Deleted);
                
                if (!isProjectMember)
                {
                    throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
                }
            }

            // Xóa các file đính kèm từ Google Drive
            if (data.Attachments != null)
            {
                try
                {
                    var attachments = JsonSerializer.Deserialize<List<DTO.AttachmentInfo>>(data.Attachments.RootElement.ToString());
                    if (attachments != null && attachments.Any())
                    {
                        var linksToDelete = attachments.Select(a => a.WebContentLink).ToList();
                        await _googleDriveService.DeleteFilesByLinks(linksToDelete);
                    }
                }
                catch (Exception ex)
                {
                    // Log lỗi nhưng vẫn tiếp tục xóa bản ghi
                    Console.WriteLine($"Failed to delete attachments: {ex.Message}");
                }
            }

            // Xóa mềm SiteSurvey
            data.Deleted = true;
            data.UpdatedAt = DateTime.UtcNow;
            data.Updater = actionBy;

            _context.Update(data);
            await _context.SaveChangesAsync();

            // Xóa cache liên quan
            _ = _cacheService.DeleteAsync(RedisCacheKey.SITE_SURVEY_CACHE_KEY);
            
            // Xóa cache theo project
            string projectCacheKey = string.Format(SITE_SURVEY_BY_PROJECT_CACHE_KEY, data.ProjectId);
            _ = _cacheService.DeleteAsync(projectCacheKey);
            
            // Xóa cache của người dùng
            var projectUsers = await _context.ProjectUsers
                .Where(pu => pu.ProjectId == data.ProjectId && !pu.Deleted)
                .ToListAsync();
                
            foreach (var pu in projectUsers)
            {
                string userCacheKey = string.Format(SITE_SURVEY_BY_USER_CACHE_KEY, pu.UserId);
                _ = _cacheService.DeleteAsync(userCacheKey);
            }

            return data.Id;
        }

        public async Task<SiteSurvey> GetSiteSurveyDetail(int projectId, int actionBy)
        {
            // Chỉ Executive Board có thể xem tất cả các project
            bool isExecutiveBoard = _helpService.IsInRole(actionBy, RoleConstValue.EXECUTIVE_BOARD);
            
            // Kiểm tra người dùng có thuộc project không
            bool hasAccess = isExecutiveBoard || await _context.ProjectUsers
                .AnyAsync(pu => pu.ProjectId == projectId && pu.UserId == actionBy && !pu.Deleted);
                
            if (!hasAccess)
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Kiểm tra cache theo project trước
            string projectCacheKey = string.Format(SITE_SURVEY_BY_PROJECT_CACHE_KEY, projectId);
            var projectSurvey = await _cacheService.GetAsync<SiteSurvey>(projectCacheKey);
            
            if (projectSurvey != null)
            {
                return projectSurvey;
            }

            // Kiểm tra cache theo user
            string userCacheKey = string.Format(SITE_SURVEY_BY_USER_CACHE_KEY, actionBy);
            var userSurveys = await _cacheService.GetAsync<List<SiteSurvey>>(userCacheKey);
            
            if (userSurveys != null)
            {
                var surveyFromCache = userSurveys.FirstOrDefault(s => s.ProjectId == projectId);
                if (surveyFromCache != null)
                {
                    return surveyFromCache;
                }
            }

            // Nếu không có trong cache, tìm trong database
            var survey = await _context.SiteSurveys.FirstOrDefaultAsync(s => s.ProjectId == projectId && !s.Deleted);
            if (survey == null)
            {
                throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
            }

            // Cập nhật cache
            _ = _cacheService.SetAsync(projectCacheKey, survey, TimeSpan.FromMinutes(30));
            
            if (userSurveys != null)
            {
                userSurveys.Add(survey);
                _ = _cacheService.SetAsync(userCacheKey, userSurveys, TimeSpan.FromMinutes(30));
            }
            else
            {
                userSurveys = new List<SiteSurvey> { survey };
                _ = _cacheService.SetAsync(userCacheKey, userSurveys, TimeSpan.FromMinutes(30));
            }

            return survey;
        }

        public async Task<SiteSurvey> SaveSiteSurvey(SaveSiteSurveyDTO model, int actionBy)
        {
            var errors = new List<ResponseError>();
            
            // Kiểm tra vai trò người dùng
            bool isExecutiveBoard = _helpService.IsInRole(actionBy, RoleConstValue.EXECUTIVE_BOARD);
            bool isTechnicalManager = _helpService.IsInRole(actionBy, RoleConstValue.TECHNICAL_MANAGER);
            
            if (!isExecutiveBoard && !isTechnicalManager)
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            
            // Nếu là Technical Manager, kiểm tra có thuộc project không
            if (isTechnicalManager && !isExecutiveBoard)
            {
                var isProjectMember = await _context.ProjectUsers
                    .AnyAsync(pu => pu.ProjectId == model.ProjectId && pu.UserId == actionBy && !pu.Deleted);
                
                if (!isProjectMember)
                {
                    throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
                }
            }
            
            // Kiểm tra xem project có tồn tại không
            var projectList = await _dataService.ListProject(new DTO.Project.SearchProjectDTO { ActionBy = actionBy, PageSize = int.MaxValue });
            var project = projectList.FirstOrDefault(t => t.Id == model.ProjectId);
            if (project == null)
            {
                throw new KeyNotFoundException(Message.SiteSurveyMessage.PROJECT_NOT_FOUND);
            }
            
            // Validation
            if (string.IsNullOrWhiteSpace(model.SiteSurveyName))
                errors.Add(new ResponseError
                {
                    Message = Message.CommonMessage.MISSING_PARAM,
                    Field = nameof(model.SiteSurveyName).ToCamelCase()
                });

            if (errors.Count > 0)
                throw new ValidationException(errors);

            // Xử lý file đính kèm
            List<AttachmentInfo> attachmentInfos = new List<AttachmentInfo>();
            string existingAttachmentsJson = null;

            if (model.Id != 0)
            {
                // Nếu là cập nhật, lấy bản ghi cũ để kiểm tra các tệp đính kèm cũ
                var existingSurvey = await _context.SiteSurveys.FirstOrDefaultAsync(t => t.Id == model.Id);
                if (existingSurvey?.Attachments != null)
                {
                    existingAttachmentsJson = existingSurvey.Attachments.RootElement.ToString();
                    attachmentInfos = JsonSerializer.Deserialize<List<AttachmentInfo>>(existingAttachmentsJson);
                }
            }

            if (model.Attachments != null && model.Attachments.Any())
            {
                // Nếu có tệp đính kèm hiện có và chúng tôi đang tải lên các tệp mới, hãy xóa các tệp cũ
                if (attachmentInfos != null && attachmentInfos.Any())
                {
                    try
                    {
                        var linksToDelete = attachmentInfos.Select(a => a.WebContentLink).ToList();
                        await _googleDriveService.DeleteFilesByLinks(linksToDelete);
                        attachmentInfos.Clear();
                    }
                    catch (Exception ex)
                    {
                        // Log lỗi nhưng vẫn tiếp tục tải lên
                        Console.WriteLine($"Failed to delete old attachments: {ex.Message}");
                    }
                }

                // Tải lên các tệp mới
                foreach (var file in model.Attachments)
                {
                    using (var stream = file.OpenReadStream())
                    {
                        var uploadResult = await _googleDriveService.UploadFile(
                            stream,
                            file.FileName,
                            file.ContentType
                        );

                        // Phân tích cú pháp phản hồi Google Drive để lấy ID tệp
                        var fileId = uploadResult.Split("id=").Last().Split("&").First();
                        
                        attachmentInfos.Add(new AttachmentInfo
                        {
                            Id = fileId,
                            Name = file.FileName,
                            WebViewLink = $"https://drive.google.com/file/d/{fileId}/view",
                            WebContentLink = uploadResult
                        });
                    }
                }
            }

            // Tạo đối tượng SiteSurvey từ DTO
            var survey = new SiteSurvey
            {
                Id = model.Id,
                ProjectId = model.ProjectId,
                SiteSurveyName = model.SiteSurveyName,
                ConstructionRequirements = model.ConstructionRequirements,
                EquipmentRequirements = model.EquipmentRequirements,
                HumanResourceCapacity = model.HumanResourceCapacity,
                RiskAssessment = model.RiskAssessment,
                BiddingDecision = model.BiddingDecision,
                ProfitAssessment = model.ProfitAssessment,
                BidWinProb = model.BidWinProb,
                EstimatedExpenses = model.EstimatedExpenses,
                EstimatedProfits = model.EstimatedProfits,
                TenderPackagePrice = model.TenderPackagePrice,
                TotalBidPrice = model.TotalBidPrice,
                DiscountRate = model.DiscountRate,
                ProjectCost = model.ProjectCost,
                FinalProfit = model.FinalProfit,
                Status = model.Status,
                Comments = model.Comments,
                Attachments = attachmentInfos.Any() ? JsonDocument.Parse(JsonSerializer.Serialize(attachmentInfos)) : null,
                SurveyDate = model.SurveyDate,
                UpdatedAt = DateTime.UtcNow,
                Updater = actionBy,
                Deleted = false
            };

            if (model.Id != 0)
            {
                // Cập nhật
                var exist = await _context.SiteSurveys.FirstOrDefaultAsync(s => s.Id == model.Id && !s.Deleted);
                if (exist == null)
                {
                    throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
                }
                
                survey.CreatedAt = exist.CreatedAt;
                survey.Creator = exist.Creator;
                _context.Update(survey);
            }
            else
            {
                // Tạo mới
                survey.CreatedAt = DateTime.UtcNow;
                survey.Creator = actionBy;
                await _context.AddAsync(survey);
            }

            await _context.SaveChangesAsync();
            
            // Xóa cache liên quan
            _ = _cacheService.DeleteAsync(RedisCacheKey.SITE_SURVEY_CACHE_KEY);
            
            // Xóa cache theo project
            string projectCacheKey = string.Format(SITE_SURVEY_BY_PROJECT_CACHE_KEY, model.ProjectId);
            _ = _cacheService.DeleteAsync(projectCacheKey);
            
            // Xóa cache của người dùng liên quan đến project
            var projectUsers = await _context.ProjectUsers
                .Where(pu => pu.ProjectId == model.ProjectId && !pu.Deleted)
                .ToListAsync();
                
            foreach (var pu in projectUsers)
            {
                string userCacheKey = string.Format(SITE_SURVEY_BY_USER_CACHE_KEY, pu.UserId);
                _ = _cacheService.DeleteAsync(userCacheKey);
            }

            return survey;
        }
    }
}
