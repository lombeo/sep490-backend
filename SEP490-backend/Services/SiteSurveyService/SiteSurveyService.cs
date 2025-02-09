using Microsoft.EntityFrameworkCore;
using Sep490_Backend.DTO.SiteSurveyDTO;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Services.CacheService;
using Sep490_Backend.Services.HelperService;
using System;

namespace Sep490_Backend.Services.SiteSurveyService
{
    public interface ISiteSurveyService
    {
        Task<List<SiteSurvey>> Search(SearchSiteSurveyDTO model);
        Task<SiteSurvey> SaveSiteSurvey(SiteSurvey model, int actionBy);
        Task<int> DeleteSiteSurvey(int id, int actionBy);
        Task<SiteSurvey> GetSiteSurveyDetail(int id, int actionBy);
    }

    public class SiteSurveyService : ISiteSurveyService
    {
        private readonly BackendContext _context;
        private readonly ICacheService _cacheService;
        private readonly IHelperService _helpService;

        public SiteSurveyService(BackendContext context, ICacheService cacheService, IHelperService helpService)
        {
            _context = context;
            _cacheService = cacheService;
            _helpService = helpService;
        }

        public async Task<int> DeleteSiteSurvey(int id, int actionBy)
        {
            if (!_helpService.IsInRole(actionBy, RoleConstValue.TECHNICAL_MANAGER))
            {
                throw new ApplicationException(Message.CommonMessage.NOT_ALLOWED);
            }
            var data = await _context.SiteSurveys.FirstOrDefaultAsync(t => t.Id == id && !t.Deleted);
            if (data == null)
            {
                throw new ApplicationException(Message.CommonMessage.NOT_FOUND);
            }

            data.Deleted = true;

            _context.Update(data);
            await _context.SaveChangesAsync();
            _ = _cacheService.DeleteAsync(RedisCacheKey.SITE_SURVEY_CACHE_KEY);

            return data.Id;
        }

        public async Task<SiteSurvey> GetSiteSurveyDetail(int id, int actionBy)
        {
            if (!_helpService.IsInRole(actionBy, new List<string> { RoleConstValue.TECHNICAL_MANAGER, RoleConstValue.EXECUTIVE_BOARD }))
            {
                throw new ApplicationException(Message.CommonMessage.NOT_ALLOWED);
            }
            var data = await Search(new SearchSiteSurveyDTO());
            return data.FirstOrDefault(t => t.Id == id);
        }

        public async Task<SiteSurvey> SaveSiteSurvey(SiteSurvey model, int actionBy)
        {
            if (!_helpService.IsInRole(actionBy, RoleConstValue.TECHNICAL_MANAGER))
            {
                throw new ApplicationException(Message.CommonMessage.NOT_ALLOWED);
            }
            var project = await _context.Projects.FirstOrDefaultAsync(t => t.Id == model.ProjectId);
            if (project == null)
            {
                throw new ApplicationException(Message.SiteSurveyMessage.PROJECT_NOT_FOUND);
            }
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
                var exist = await GetSiteSurveyDetail(model.Id, actionBy);
                if (exist == null)
                {
                    throw new ApplicationException(Message.CommonMessage.NOT_FOUND);
                }
                else
                {
                    survey.CreatedAt = exist.CreatedAt;
                    survey.Creator = exist.Creator;
                    exist = survey;
                    _context.Update(exist);
                }
            }
            else
            {
                survey.CreatedAt = DateTime.UtcNow;
                survey.Creator = actionBy;
                await _context.AddAsync(survey);
            }

            await _context.SaveChangesAsync();
            _ = _cacheService.DeleteAsync(RedisCacheKey.SITE_SURVEY_CACHE_KEY);

            return survey;
        }

        public async Task<List<SiteSurvey>> Search(SearchSiteSurveyDTO model)
        {
            if (!_helpService.IsInRole(model.ActionBy, new List<string> { RoleConstValue.TECHNICAL_MANAGER, RoleConstValue.EXECUTIVE_BOARD }))
            {
                throw new ApplicationException(Message.CommonMessage.NOT_ALLOWED);
            }
            string cacheKey = RedisCacheKey.SITE_SURVEY_CACHE_KEY;
            var data = await _cacheService.GetAsync<List<SiteSurvey>>(cacheKey);
            if (data == null)
            {
                data = await _context.SiteSurveys.Where(t => !t.Deleted).ToListAsync();
                _ = _cacheService.SetAsync(cacheKey, data);
            }
            if (!string.IsNullOrWhiteSpace(model.SiteSurveyName))
            {
                data = data.Where(t => t.SiteSurveyName.ToLower().Trim().Contains(model.SiteSurveyName.ToLower().Trim())).ToList();
            }

            if (model.Status != null)
            {
                data = data.Where(t => t.Status == model.Status).ToList();
            }

            model.Total = data.Count();

            if (model.PageSize > 0)
            {
                data = data.Skip(model.Skip).Take(model.PageSize).ToList();
            }

            return data;
        }
    }
}
