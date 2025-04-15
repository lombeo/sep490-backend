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

            // Clear the change tracker to start with a clean state
            _context.ChangeTracker.Clear();

            // Tìm SiteSurvey cần xóa
            var data = await _context.SiteSurveys
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id && !t.Deleted);
                
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

            // Attach the entity and mark it as modified to perform soft delete
            var siteSurveyToUpdate = new SiteSurvey
            {
                Id = data.Id,
                Deleted = true,
                UpdatedAt = DateTime.UtcNow,
                Updater = actionBy
            };
            
            _context.SiteSurveys.Attach(siteSurveyToUpdate);
            _context.Entry(siteSurveyToUpdate).Property(x => x.Deleted).IsModified = true;
            _context.Entry(siteSurveyToUpdate).Property(x => x.UpdatedAt).IsModified = true;
            _context.Entry(siteSurveyToUpdate).Property(x => x.Updater).IsModified = true;
            
            await _context.SaveChangesAsync();
            
            // Clear tracking after save
            _context.ChangeTracker.Clear();

            // Xóa cache cho SiteSurvey
            await InvalidateSiteSurveyCaches(data.Id, data.ProjectId);

            return data.Id;
        }

        /// <summary>
        /// Xóa cache liên quan đến SiteSurvey
        /// </summary>
        private async Task InvalidateSiteSurveyCaches(int siteSurveyId, int projectId)
        {
            // Specific cache keys
            var specificCacheKeys = new List<string>
            {
                string.Format(RedisCacheKey.SITE_SURVEY_BY_ID_CACHE_KEY, siteSurveyId),
                string.Format(RedisCacheKey.SITE_SURVEY_BY_PROJECT_CACHE_KEY, projectId)
            };
            
            // Main cache keys to invalidate
            var mainCacheKeys = new[]
            {
                RedisCacheKey.SITE_SURVEY_CACHE_KEY,
                RedisCacheKey.SITE_SURVEY_LIST_CACHE_KEY,
                RedisCacheKey.PROJECT_CACHE_KEY 
            };
            
            // Delete specific cache keys
            foreach (var cacheKey in specificCacheKeys)
            {
                await _cacheService.DeleteAsync(cacheKey);
            }
            
            // Delete main cache keys
            foreach (var cacheKey in mainCacheKeys)
            {
                await _cacheService.DeleteAsync(cacheKey);
            }
            
            // Delete pattern-based caches
            await _cacheService.DeleteByPatternAsync(RedisCacheKey.SITE_SURVEY_ALL_PATTERN);
        }

        public async Task<SiteSurvey> GetSiteSurveyDetail(int projectId, int actionBy)
        {
            // Check authorization
            bool isExecutiveBoard = _helpService.IsInRole(actionBy, RoleConstValue.EXECUTIVE_BOARD);
            
            // Check if user has access to this project
            bool hasAccess = isExecutiveBoard || await _context.ProjectUsers
                .AsNoTracking()
                .AnyAsync(pu => pu.ProjectId == projectId && pu.UserId == actionBy && !pu.Deleted);
                
            if (!hasAccess)
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            
            // Try to get from project-specific site survey cache
            string projectSurveyKey = string.Format(RedisCacheKey.SITE_SURVEY_BY_PROJECT_CACHE_KEY, projectId);
            var surveyFromProjectCache = await _cacheService.GetAsync<SiteSurvey>(projectSurveyKey);
            
            if (surveyFromProjectCache != null)
            {
                return surveyFromProjectCache;
            }

            // If not in project-specific cache, check main cache
            var allSurveys = await _cacheService.GetAsync<List<SiteSurvey>>(RedisCacheKey.SITE_SURVEY_CACHE_KEY);
            
            if (allSurveys != null)
            {
                // Filter from main cache by projectId
                var surveyFromMainCache = allSurveys.FirstOrDefault(s => s.ProjectId == projectId && !s.Deleted);
                if (surveyFromMainCache != null)
                {
                    // Store in project-specific cache for faster retrieval next time
                    await _cacheService.SetAsync(projectSurveyKey, surveyFromMainCache, TimeSpan.FromHours(1));
                    return surveyFromMainCache;
                }
            }

            // Clear the change tracker before querying the database
            _context.ChangeTracker.Clear();

            // If not found in any cache, get from database
            var survey = await _context.SiteSurveys
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ProjectId == projectId && !s.Deleted);
                
            if (survey == null)
            {
                throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
            }

            // Cache the retrieved survey
            await _cacheService.SetAsync(projectSurveyKey, survey, TimeSpan.FromHours(1));
            
            // Update main cache if needed
            await UpdateSurveyInMainCache(survey);
            
            return survey;
        }
        
        // Helper method to update site survey in main cache
        private async Task UpdateSurveyInMainCache(SiteSurvey survey)
        {
            var allSurveys = await _cacheService.GetAsync<List<SiteSurvey>>(RedisCacheKey.SITE_SURVEY_CACHE_KEY);
            
            if (allSurveys == null)
            {
                // Clear the change tracker before querying
                _context.ChangeTracker.Clear();
                
                // If main cache is empty, load all surveys
                allSurveys = await _context.SiteSurveys
                    .AsNoTracking()
                    .Where(s => !s.Deleted)
                    .ToListAsync();
                    
                await _cacheService.SetAsync(RedisCacheKey.SITE_SURVEY_CACHE_KEY, allSurveys, TimeSpan.FromHours(1));
                await _cacheService.SetAsync(RedisCacheKey.SITE_SURVEY_LIST_CACHE_KEY, allSurveys, TimeSpan.FromHours(1));
            }
            else
            {
                // Find and update the survey in cache
                var existingSurvey = allSurveys.FirstOrDefault(s => s.Id == survey.Id);
                if (existingSurvey != null)
                {
                    // Update existing survey
                    int index = allSurveys.IndexOf(existingSurvey);
                    allSurveys[index] = survey;
                }
                else
                {
                    // Add new survey to cache
                    allSurveys.Add(survey);
                }
                
                // Update both cache keys
                await _cacheService.SetAsync(RedisCacheKey.SITE_SURVEY_CACHE_KEY, allSurveys, TimeSpan.FromHours(1));
                await _cacheService.SetAsync(RedisCacheKey.SITE_SURVEY_LIST_CACHE_KEY, allSurveys, TimeSpan.FromHours(1));
            }
            
            // Also cache in the survey by ID cache
            string surveyByIdKey = string.Format(RedisCacheKey.SITE_SURVEY_BY_ID_CACHE_KEY, survey.Id);
            await _cacheService.SetAsync(surveyByIdKey, survey, TimeSpan.FromHours(1));
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
                // Clear the change tracker to prevent tracking conflicts
                _context.ChangeTracker.Clear();
                
                // Cập nhật - Get existing record without tracking
                var exist = await _context.SiteSurveys
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == model.Id && !s.Deleted);
                    
                if (exist == null)
                {
                    throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
                }
                
                survey.CreatedAt = exist.CreatedAt;
                survey.Creator = exist.Creator;
                
                // Set entity state explicitly
                _context.Entry(survey).State = EntityState.Modified;
            }
            else
            {
                // Tạo mới
                survey.CreatedAt = DateTime.UtcNow;
                survey.Creator = actionBy;
                await _context.AddAsync(survey);
            }

            await _context.SaveChangesAsync();
            
            // Clear tracking after save to avoid conflicts with future operations
            _context.ChangeTracker.Clear();
            
            // Invalidate all related caches
            await InvalidateSiteSurveyCaches(survey.Id, survey.ProjectId);

            return survey;
        }
    }
}
