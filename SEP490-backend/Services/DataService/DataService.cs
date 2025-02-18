using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Sep490_Backend.DTO.Admin;
using Sep490_Backend.DTO.Contract;
using Sep490_Backend.DTO.Customer;
using Sep490_Backend.DTO.Project;
using Sep490_Backend.DTO.SiteSurvey;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Services.CacheService;
using Sep490_Backend.Services.HelperService;

namespace Sep490_Backend.Services.DataService
{
    public interface IDataService
    {
        Task<List<ContractDTO>> ListContract(SearchContractDTO model);
        Task<List<User>> ListUser(AdminSearchUserDTO model);
        Task<List<Customer>> ListCustomer(CustomerSearchDTO model);
        Task<List<ProjectDTO>> ListProject(SearchProjectDTO model);
        Task<List<SiteSurvey>> ListSiteSurvey(SearchSiteSurveyDTO model);
    }

    public class DataService : IDataService
    {
        private readonly BackendContext _context;
        private readonly IHelperService _helpService;
        private readonly ICacheService _cacheService;

        public DataService(BackendContext context, IHelperService helpService, ICacheService cacheService)
        {
            _context = context;
            _helpService = helpService;
            _cacheService = cacheService;
        }

        public async Task<List<ContractDTO>> ListContract(SearchContractDTO model)
        {
            if (!_helpService.IsInRole(model.ActionBy, new List<string> { RoleConstValue.BUSINESS_EMPLOYEE, RoleConstValue.EXECUTIVE_BOARD }))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            string cacheKey = RedisCacheKey.CUSTOMER_CACHE_KEY;
            var data = await _cacheService.GetAsync<List<ContractDTO>>(cacheKey);

            if(data == null)
            {
                var project = await ListProject(new SearchProjectDTO()
                {
                    ActionBy = model.ActionBy
                });
                data = _context.Contracts.Where(t => !t.Deleted).Select(t => new ContractDTO
                {
                    Id = t.Id,
                    ContractCode = t.ContractCode,
                    Project = project.FirstOrDefault(p => p.Id == t.ProjectId) ?? new ProjectDTO(),
                    StartDate = t.StartDate,
                    EndDate = t.EndDate,
                    EstimatedDays = t.EstimatedDays,
                    Status = t.Status,
                    Tax = t.Tax,
                    SignDate = t.SignDate,
                    Attachment = t.Attachment,
                    UpdatedAt = t.UpdatedAt,
                    Updater = t.Updater,
                    CreatedAt = t.CreatedAt,
                    Creator = t.Creator,
                    Deleted = t.Deleted
                }).ToList();

                _ = _cacheService.SetAsync(cacheKey, data);
            }

            if (!string.IsNullOrWhiteSpace(model.KeyWord))
            {
                data = data.Where(t => t.ContractCode.ToLower().Trim().Contains(model.KeyWord.ToLower().Trim())
                                    || t.Project.ProjectCode.ToLower().Trim().Contains(model.KeyWord.ToLower().Trim())
                                    || t.Project.ProjectName.ToLower().Trim().Contains(model.KeyWord.ToLower().Trim())).ToList();
            }

            if(model.ProjectId != 0)
            {
                data = data.Where(t => t.Project.Id == model.ProjectId).ToList();
            }
            if(model.Status != null)
            {
                data = data.Where(t => t.Status == model.Status).ToList();
            }
            if(model.SignDate != null)
            {
                data = data.Where(t => t.SignDate == model.SignDate).ToList();
            }

            model.Total = data.Count();

            if (model.PageSize > 0)
            {
                data = data.Skip(model.Skip).Take(model.PageSize).ToList();
            }

            return data;
        }

        public async Task<List<Customer>> ListCustomer(CustomerSearchDTO model)
        {
            if (!_helpService.IsInRole(model.ActionBy, new List<string> { RoleConstValue.BUSINESS_EMPLOYEE, RoleConstValue.EXECUTIVE_BOARD }))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            string cacheKey = RedisCacheKey.CUSTOMER_CACHE_KEY;
            var customerCacheList = await _cacheService.GetAsync<List<Customer>>(cacheKey);
            if (customerCacheList == null)
            {
                customerCacheList = await _context.Customers.Where(c => !c.Deleted).ToListAsync();
                _ = _cacheService.SetAsync(cacheKey, customerCacheList);
            }
            if (!string.IsNullOrWhiteSpace(model.Search))
            {
                customerCacheList = customerCacheList.Where(t => t.CustomerName.ToLower().Trim().Contains(model.Search.ToLower().Trim())
                || t.CustomerCode.ToLower().Trim().Contains(model.Search.ToLower().Trim())
                || t.Phone.ToLower().Trim().Contains(model.Search.ToLower().Trim())).ToList();
            }
            model.Total = customerCacheList.Count();
            if (model.PageSize > 0)
            {
                customerCacheList = customerCacheList.Skip(model.Skip).Take(model.PageSize).ToList();
            }
            return customerCacheList;
        }

        public async Task<List<ProjectDTO>> ListProject(SearchProjectDTO model)
        {
            if (!_helpService.IsInRole(model.ActionBy, RoleConstValue.BUSINESS_EMPLOYEE))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            string cacheKey = RedisCacheKey.PROJECT_CACHE_KEY;
            var data = await _cacheService.GetAsync<List<ProjectDTO>>(cacheKey);
            if (data == null)
            {
                data = _context.Projects.Where(t => !t.Deleted).Select(t => new ProjectDTO
                {
                    Id = t.Id,
                    ProjectCode = t.ProjectCode,
                    ProjectName = t.ProjectName,
                    Customer = _context.Customers.FirstOrDefault(c => c.Id == t.CustomerId) ?? new Customer(),
                    ConstructType = t.ConstructType,
                    Location = t.Location,
                    Area = t.Area,
                    Purpose = t.Purpose,
                    TechnicalReqs = t.TechnicalReqs,
                    StartDate = t.StartDate,
                    EndDate = t.EndDate,
                    Budget = t.Budget,
                    Status = t.Status,
                    Attachment = t.Attachment,
                    Description = t.Description,
                    UpdatedAt = t.UpdatedAt,
                    Updater = t.Updater,
                    CreatedAt = t.CreatedAt,
                    Creator = t.Creator,
                    Deleted = t.Deleted
                }).ToList();

                _ = _cacheService.SetAsync(cacheKey, data);
            }
            if (!string.IsNullOrWhiteSpace(model.KeyWord))
            {
                data = data.Where(t => t.ProjectCode.ToLower().Trim().Contains(model.KeyWord.ToLower().Trim())
                                        || t.ProjectName.ToLower().Trim().Contains(model.KeyWord.ToLower().Trim())
                                        || t.Customer.CustomerCode.ToLower().Trim().Contains(model.KeyWord.ToLower().Trim())
                                        || (t.Location ?? "").ToLower().Trim().Contains(model.KeyWord.ToLower().Trim())
                                        || t.Customer.CustomerName.ToLower().Trim().Contains(model.KeyWord.ToLower().Trim())).ToList();
            }
            if (model.CustomerId != 0)
            {
                data = data.Where(t => t.Customer.Id == model.CustomerId).ToList();
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

        public async Task<List<SiteSurvey>> ListSiteSurvey(SearchSiteSurveyDTO model)
        {
            if (!_helpService.IsInRole(model.ActionBy, new List<string> { RoleConstValue.TECHNICAL_MANAGER, RoleConstValue.EXECUTIVE_BOARD }))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
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

        public async Task<List<User>> ListUser(AdminSearchUserDTO model)
        {
            var data = StaticVariable.UserMemory.ToList();
            if (!_helpService.IsInRole(model.ActionBy, new List<string> { RoleConstValue.ADMIN }))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            data = data.OrderByDescending(t => t.CreatedAt).ToList();

            if (!string.IsNullOrWhiteSpace(model.KeyWord))
            {
                data = data.Where(t => t.FullName.Contains(model.KeyWord) || t.Username.Contains(model.KeyWord) || t.Email.Contains(model.KeyWord) || t.Phone.Contains(model.KeyWord)).ToList();
            }
            if (!string.IsNullOrWhiteSpace(model.Role))
            {
                data = data.Where(t => t.Role == model.Role).ToList();
            }
            if (model.Gender != null)
            {
                data = data.Where(t => t.Gender == model.Gender).ToList();
            }
            if (model.Dob != null)
            {
                data = data.Where(t => t.Dob == model.Dob).ToList();
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
