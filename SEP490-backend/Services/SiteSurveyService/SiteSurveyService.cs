using Microsoft.EntityFrameworkCore;
using Sep490_Backend.Controllers;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.SiteSurvey;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Infra.Helps;
using Sep490_Backend.Services.CacheService;
using Sep490_Backend.Services.DataService;
using Sep490_Backend.Services.HelperService;
using System;

namespace Sep490_Backend.Services.SiteSurveyService
{
    public interface ISiteSurveyService
    {
        Task<SiteSurvey> SaveSiteSurvey(SiteSurvey model, int actionBy);
        Task<int> DeleteSiteSurvey(int id, int actionBy);
        Task<SiteSurvey> GetSiteSurveyDetail(int id, int actionBy);
    }

    public class SiteSurveyService : ISiteSurveyService
    {
        private readonly BackendContext _context;
        private readonly ICacheService _cacheService;
        private readonly IHelperService _helpService;
        private readonly IDataService _dataService;

        // Định nghĩa các key cache cho SiteSurvey
        private const string SITE_SURVEY_BY_PROJECT_CACHE_KEY = "SITE_SURVEY:PROJECT:{0}"; // Pattern: SITE_SURVEY:PROJECT:projectId
        private const string SITE_SURVEY_BY_USER_CACHE_KEY = "SITE_SURVEY:USER:{0}"; // Pattern: SITE_SURVEY:USER:userId

        public SiteSurveyService(BackendContext context, IDataService dataService, ICacheService cacheService, IHelperService helpService)
        {
            _context = context;
            _cacheService = cacheService;
            _helpService = helpService;
            _dataService = dataService;
        }

        public async Task<int> DeleteSiteSurvey(int id, int actionBy)
        {
            // Kiểm tra vai trò người dùng (Technical Manager hoặc Executive Board)
            if (!_helpService.IsInRole(actionBy, new List<string> { RoleConstValue.TECHNICAL_MANAGER, RoleConstValue.EXECUTIVE_BOARD }))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Tìm SiteSurvey cần xóa
            var data = await _context.SiteSurveys.FirstOrDefaultAsync(t => t.Id == id && !t.Deleted);
            if (data == null)
            {
                throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
            }

            // Kiểm tra xem người dùng có phải là người tạo Project chứa SiteSurvey này không
            var projectCreator = await _context.ProjectUsers
                .FirstOrDefaultAsync(pu => pu.ProjectId == data.ProjectId && pu.IsCreator && !pu.Deleted);
            
            if (projectCreator == null || projectCreator.UserId != actionBy)
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
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

        public async Task<SiteSurvey> GetSiteSurveyDetail(int id, int actionBy)
        {
            // Kiểm tra vai trò người dùng
            if (!_helpService.IsInRole(actionBy, new List<string> { RoleConstValue.TECHNICAL_MANAGER, RoleConstValue.EXECUTIVE_BOARD }))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Kiểm tra cache theo user trước
            string userCacheKey = string.Format(SITE_SURVEY_BY_USER_CACHE_KEY, actionBy);
            var userSurveys = await _cacheService.GetAsync<List<SiteSurvey>>(userCacheKey);
            
            if (userSurveys != null)
            {
                var surveyFromCache = userSurveys.FirstOrDefault(s => s.Id == id);
                if (surveyFromCache != null)
                {
                    return surveyFromCache;
                }
            }

            // Nếu không có trong cache, tìm trong database
            var survey = await _context.SiteSurveys.FirstOrDefaultAsync(s => s.Id == id && !s.Deleted);
            if (survey == null)
            {
                throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
            }

            // Kiểm tra quyền truy cập (người dùng phải là thành viên của project chứa site survey này)
            var hasAccess = await _context.ProjectUsers
                .AnyAsync(pu => pu.ProjectId == survey.ProjectId && pu.UserId == actionBy && !pu.Deleted);
                
            if (!hasAccess)
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Cập nhật cache nếu cần
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

        public async Task<SiteSurvey> SaveSiteSurvey(SiteSurvey model, int actionBy)
        {
            var errors = new List<ResponseError>();
            
            // Kiểm tra vai trò người dùng
            if (!_helpService.IsInRole(actionBy, new List<string> { RoleConstValue.TECHNICAL_MANAGER, RoleConstValue.EXECUTIVE_BOARD }))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            
            // Kiểm tra xem project có tồn tại không
            var projectList = await _dataService.ListProject(new DTO.Project.SearchProjectDTO { ActionBy = actionBy, PageSize = int.MaxValue });
            var project = projectList.FirstOrDefault(t => t.Id == model.ProjectId);
            if (project == null)
            {
                throw new KeyNotFoundException(Message.SiteSurveyMessage.PROJECT_NOT_FOUND);
            }
            
            // Kiểm tra xem người dùng có phải là người tạo Project không
            var isProjectCreator = await _context.ProjectUsers
                .AnyAsync(pu => pu.ProjectId == model.ProjectId && pu.UserId == actionBy && pu.IsCreator && !pu.Deleted);
                
            if (!isProjectCreator)
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            
            // Validation
            if (string.IsNullOrWhiteSpace(model.SiteSurveyName))
                errors.Add(new ResponseError
                {
                    Message = Message.CommonMessage.MISSING_PARAM,
                    Field = nameof(model.SiteSurveyName).ToCamelCase()
                });
            if (model.SurveyDate == DateTime.MinValue)
                errors.Add(new ResponseError
                {
                    Message = Message.CommonMessage.MISSING_PARAM,
                    Field = nameof(model.SurveyDate).ToCamelCase()
                });

            if (errors.Count > 0)
                throw new ValidationException(errors);

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
                Attachments = model.Attachments,
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
