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
using System.Text.Json;

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

            // Xác định các key cache
            string generalCacheKey = RedisCacheKey.CONTRACT_CACHE_KEY;
            string userCacheKey = string.Format("CONTRACT:USER:{0}", model.ActionBy);
            
            // Thử lấy từ cache của user trước
            var userCache = await _cacheService.GetAsync<List<ContractDTO>>(userCacheKey);
            if (userCache != null)
            {
                var filteredData = ApplyContractFilters(userCache, model);
                model.Total = filteredData.Count();
                
                if (model.PageSize > 0)
                {
                    filteredData = filteredData.Skip(model.Skip).Take(model.PageSize).ToList();
                }
                
                return filteredData;
            }

            // Nếu không có trong cache của user, thử lấy từ cache chung
            var data = await _cacheService.GetAsync<List<ContractDTO>>(generalCacheKey);
            if (data == null)
            {
                // Lấy danh sách Project mà người dùng có quyền truy cập
                var project = await ListProject(new SearchProjectDTO()
                {
                    ActionBy = model.ActionBy,
                    PageSize = int.MaxValue
                });
                
                var projectIds = project.Select(p => p.Id).ToList();
                
                // Lấy danh sách Contract thuộc các Project mà người dùng có quyền truy cập
                var contracts = await _context.Contracts
                    .Where(t => !t.Deleted && projectIds.Contains(t.ProjectId))
                    .ToListAsync();
                
                // Lấy tất cả ContractDetail không bị xóa
                var allContractDetails = await _context.Set<ContractDetail>()
                    .Where(cd => !cd.Deleted && contracts.Select(c => c.Id).Contains(cd.ContractId))
                    .ToListAsync();
                
                data = contracts.Select(t => new ContractDTO
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
                    Attachments = t.Attachments != null ? 
                        System.Text.Json.JsonSerializer.Deserialize<List<AttachmentInfo>>(t.Attachments.RootElement.ToString()) 
                        : null,
                    UpdatedAt = t.UpdatedAt,
                    Updater = t.Updater,
                    CreatedAt = t.CreatedAt,
                    Creator = t.Creator,
                    Deleted = t.Deleted,
                    // Thêm danh sách ContractDetail cho mỗi Contract
                    ContractDetails = allContractDetails
                        .Where(cd => cd.ContractId == t.Id)
                        .Select(cd => new ContractDetailDTO
                        {
                            WorkCode = cd.WorkCode,
                            Index = cd.Index,
                            ContractId = cd.ContractId,
                            ParentIndex = cd.ParentIndex,
                            WorkName = cd.WorkName,
                            Unit = cd.Unit,
                            Quantity = cd.Quantity,
                            UnitPrice = cd.UnitPrice,
                            CreatedAt = cd.CreatedAt,
                            Creator = cd.Creator,
                            UpdatedAt = cd.UpdatedAt,
                            Updater = cd.Updater,
                            Deleted = cd.Deleted
                        }).ToList()
                }).OrderByDescending(t => t.UpdatedAt).ToList();

                // Lưu vào cache chung
                _ = _cacheService.SetAsync(generalCacheKey, data, TimeSpan.FromMinutes(30));
            }
            else
            {
                // Nếu có trong cache chung, lọc theo quyền truy cập của người dùng
                var projectIds = await _context.ProjectUsers
                    .Where(pu => pu.UserId == model.ActionBy && !pu.Deleted)
                    .Select(pu => pu.ProjectId)
                    .ToListAsync();
                    
                data = data.Where(c => projectIds.Contains(c.Project.Id)).ToList();
            }
            
            // Lưu vào cache của user
            _ = _cacheService.SetAsync(userCacheKey, data, TimeSpan.FromMinutes(30));
            
            // Áp dụng bộ lọc theo điều kiện search
            var result = ApplyContractFilters(data, model);
            model.Total = result.Count();
            
            if (model.PageSize > 0)
            {
                result = result.Skip(model.Skip).Take(model.PageSize).ToList();
            }
            
            return result;
        }
        
        private List<ContractDTO> ApplyContractFilters(List<ContractDTO> data, SearchContractDTO model)
        {
            var result = data;
            
            if (!string.IsNullOrWhiteSpace(model.KeyWord))
            {
                result = result.Where(t => t.ContractCode.ToLower().Trim().Contains(model.KeyWord.ToLower().Trim())
                                     || t.Project.ProjectCode.ToLower().Trim().Contains(model.KeyWord.ToLower().Trim())
                                     || t.Project.ProjectName.ToLower().Trim().Contains(model.KeyWord.ToLower().Trim())).ToList();
            }

            if(model.ProjectId != 0)
            {
                result = result.Where(t => t.Project.Id == model.ProjectId).ToList();
            }
            
            if(model.Status != null)
            {
                result = result.Where(t => t.Status == model.Status).ToList();
            }
            
            if(model.SignDate != null)
            {
                result = result.Where(t => t.SignDate == model.SignDate).ToList();
            }
            
            return result;
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
                customerCacheList = await _context.Customers.Where(c => !c.Deleted).OrderByDescending(t => t.UpdatedAt).ToListAsync();
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

            // Tạo cache key riêng cho từng user để lưu trữ danh sách project theo phân quyền
            string userProjectCacheKey = string.Format(RedisCacheKey.PROJECT_BY_USER_CACHE_KEY, model.ActionBy);
            
            // Thử lấy danh sách project từ cache theo user
            var userProjects = await _cacheService.GetAsync<List<ProjectDTO>>(userProjectCacheKey);
            
            if (userProjects == null)
            {
                // Không có trong cache, cần tạo mới
                
                // 1. Lấy danh sách project từ cache chung hoặc từ database
                string projectCacheKey = RedisCacheKey.PROJECT_CACHE_KEY;
                var allProjects = await _cacheService.GetAsync<List<Project>>(projectCacheKey);
                
                if (allProjects == null)
                {
                    // Lấy từ database và cache lại
                    allProjects = await _context.Projects
                        .Where(t => !t.Deleted)
                        .ToListAsync();
                    
                    _ = _cacheService.SetAsync(projectCacheKey, allProjects);
                }
                
                // 2. Lấy danh sách phân quyền project từ cache hoặc từ database
                string projectUserCacheKey = RedisCacheKey.PROJECT_USER_CACHE_KEY;
                var allProjectPermissions = await _cacheService.GetAsync<List<ProjectUser>>(projectUserCacheKey);
                
                if (allProjectPermissions == null)
                {
                    // Lấy từ database và cache lại
                    allProjectPermissions = await _context.ProjectUsers
                        .Where(pu => !pu.Deleted)
                        .ToListAsync();
                    
                    _ = _cacheService.SetAsync(projectUserCacheKey, allProjectPermissions);
                }
                
                // 3. Lấy thông tin về customer
                var allCustomers = await _context.Customers.Where(c => !c.Deleted).ToListAsync();
                
                // 4. Xác định những project mà user có quyền xem
                userProjects = new List<ProjectDTO>();
                
                foreach (var project in allProjects)
                {
                    // Xác định nếu người dùng hiện tại là người tạo
                    var isCreator = allProjectPermissions.Any(pu => 
                        pu.ProjectId == project.Id && 
                        pu.IsCreator && 
                        pu.UserId == model.ActionBy);
                    
                    // Xác định nếu người dùng hiện tại là người được chỉ định xem
                    var isViewer = allProjectPermissions.Any(pu => 
                        pu.ProjectId == project.Id && 
                        !pu.IsCreator && 
                        pu.UserId == model.ActionBy && 
                        !pu.Deleted);
                    
                    // Chỉ đưa vào danh sách những project mà người dùng có quyền
                    if (isCreator || isViewer)
                    {
                        // Danh sách người có quyền xem project
                        var viewerIds = allProjectPermissions
                            .Where(pu => pu.ProjectId == project.Id && !pu.IsCreator && !pu.Deleted)
                            .Select(pu => pu.UserId)
                            .ToList();
                        
                        var customer = allCustomers.FirstOrDefault(c => c.Id == project.CustomerId) ?? new Customer();
                        
                        userProjects.Add(new ProjectDTO
                        {
                            Id = project.Id,
                            ProjectCode = project.ProjectCode,
                            ProjectName = project.ProjectName,
                            Customer = customer,
                            ConstructType = project.ConstructType,
                            Location = project.Location,
                            Area = project.Area,
                            Purpose = project.Purpose,
                            TechnicalReqs = project.TechnicalReqs,
                            StartDate = project.StartDate,
                            EndDate = project.EndDate,
                            Budget = project.Budget,
                            Status = project.Status,
                            Attachments = project.Attachments != null ? 
                                JsonSerializer.Deserialize<List<AttachmentInfo>>(project.Attachments.RootElement.ToString()) 
                                : null,
                            Description = project.Description,
                            UpdatedAt = project.UpdatedAt,
                            Updater = project.Updater,
                            CreatedAt = project.CreatedAt,
                            Creator = project.Creator,
                            Deleted = project.Deleted,
                            IsCreator = isCreator,
                            ViewerUserIds = viewerIds
                        });
                    }
                }
                
                userProjects = userProjects.OrderByDescending(t => t.UpdatedAt).ToList();
                
                // Cache danh sách project của người dùng
                _ = _cacheService.SetAsync(userProjectCacheKey, userProjects, TimeSpan.FromMinutes(30)); // Cache ngắn hạn
            }
            
            // Áp dụng các bộ lọc
            var filteredData = userProjects;
            
            if (!string.IsNullOrWhiteSpace(model.KeyWord))
            {
                filteredData = filteredData.Where(t => t.ProjectCode.ToLower().Trim().Contains(model.KeyWord.ToLower().Trim())
                                        || t.ProjectName.ToLower().Trim().Contains(model.KeyWord.ToLower().Trim())
                                        || t.Customer.CustomerCode.ToLower().Trim().Contains(model.KeyWord.ToLower().Trim())
                                        || (t.Location ?? "").ToLower().Trim().Contains(model.KeyWord.ToLower().Trim())
                                        || t.Customer.CustomerName.ToLower().Trim().Contains(model.KeyWord.ToLower().Trim())).ToList();
            }
            if (model.CustomerId != 0)
            {
                filteredData = filteredData.Where(t => t.Customer.Id == model.CustomerId).ToList();
            }
            if (model.Status != null)
            {
                filteredData = filteredData.Where(t => t.Status == model.Status).ToList();
            }

            model.Total = filteredData.Count();

            if (model.PageSize > 0)
            {
                filteredData = filteredData.Skip(model.Skip).Take(model.PageSize).ToList();
            }

            return filteredData;
        }

        public async Task<List<SiteSurvey>> ListSiteSurvey(SearchSiteSurveyDTO model)
        {
            if (!_helpService.IsInRole(model.ActionBy, new List<string> { RoleConstValue.TECHNICAL_MANAGER, RoleConstValue.EXECUTIVE_BOARD }))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            
            // Xác định các key cache
            string generalCacheKey = RedisCacheKey.SITE_SURVEY_CACHE_KEY;
            string userCacheKey = string.Format("SITE_SURVEY:USER:{0}", model.ActionBy);
            
            // Thử lấy từ cache của user trước
            var userCache = await _cacheService.GetAsync<List<SiteSurvey>>(userCacheKey);
            if (userCache != null)
            {
                var filteredData = ApplyFilters(userCache, model);
                model.Total = filteredData.Count();
                
                if (model.PageSize > 0)
                {
                    filteredData = filteredData.Skip(model.Skip).Take(model.PageSize).ToList();
                }
                
                return filteredData;
            }
            
            // Nếu không có trong cache của user, thử lấy từ cache chung
            var data = await _cacheService.GetAsync<List<SiteSurvey>>(generalCacheKey);
            if (data == null)
            {
                // Nếu không có trong cache chung, query từ database
                data = await _context.SiteSurveys.Where(t => !t.Deleted).OrderByDescending(t => t.UpdatedAt).ToListAsync();
                
                // Lưu vào cache chung
                _ = _cacheService.SetAsync(generalCacheKey, data, TimeSpan.FromMinutes(30));
            }
            
            // Lọc dữ liệu theo quyền truy cập của người dùng
            // Chỉ lấy các SiteSurvey thuộc project mà người dùng là thành viên
            var projectIds = await _context.ProjectUsers
                .Where(pu => pu.UserId == model.ActionBy && !pu.Deleted)
                .Select(pu => pu.ProjectId)
                .ToListAsync();
                
            var accessibleData = data.Where(s => projectIds.Contains(s.ProjectId)).ToList();
            
            // Lưu vào cache của user
            _ = _cacheService.SetAsync(userCacheKey, accessibleData, TimeSpan.FromMinutes(30));
            
            // Áp dụng bộ lọc theo điều kiện search
            var result = ApplyFilters(accessibleData, model);
            model.Total = result.Count();
            
            if (model.PageSize > 0)
            {
                result = result.Skip(model.Skip).Take(model.PageSize).ToList();
            }
            
            return result;
        }
        
        private List<SiteSurvey> ApplyFilters(List<SiteSurvey> data, SearchSiteSurveyDTO model)
        {
            var result = data;
            
            if (!string.IsNullOrWhiteSpace(model.SiteSurveyName))
            {
                result = result.Where(t => t.SiteSurveyName.ToLower().Trim().Contains(model.SiteSurveyName.ToLower().Trim())).ToList();
            }

            if (model.Status != null)
            {
                result = result.Where(t => t.Status == model.Status).ToList();
            }
            
            return result;
        }

        public async Task<List<User>> ListUser(AdminSearchUserDTO model)
        {
            var data = StaticVariable.UserMemory.ToList();
            data = data.OrderByDescending(t => t.UpdatedAt).ToList();
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
