using Microsoft.EntityFrameworkCore;
using Sep490_Backend.DTO;
using Sep490_Backend.DTO.Admin;
using Sep490_Backend.DTO.Contract;
using Sep490_Backend.DTO.Customer;
using Sep490_Backend.DTO.Material;
using Sep490_Backend.DTO.Project;
using Sep490_Backend.DTO.SiteSurvey;
using Sep490_Backend.DTO.ConstructionTeam;
using Sep490_Backend.DTO.ConstructionPlan;
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
        Task<List<Material>> ListMaterial(MaterialSearchDTO model);
        Task<List<ConstructionTeam>> ListConstructionTeam(ConstructionTeamSearchDTO model);
        Task<List<ConstructionPlanDTO>> ListConstructionPlan(ConstructionPlanQuery model);
    }

    public class DataService : IDataService
    {
        private readonly BackendContext _context;
        private readonly IHelperService _helpService;
        private readonly ICacheService _cacheService;
        private readonly ILogger<DataService> _logger;
        private readonly TimeSpan DEFAULT_CACHE_DURATION = TimeSpan.FromMinutes(15);

        public DataService(BackendContext context, IHelperService helpService, ICacheService cacheService, ILogger<DataService> logger)
        {
            _context = context;
            _helpService = helpService;
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task<List<ContractDTO>> ListContract(SearchContractDTO model)
        {
            // Check if user is Executive Board member
            var user = StaticVariable.UserMemory.FirstOrDefault(u => u.Id == model.ActionBy);
            bool isExecutiveBoard = user != null && user.Role == RoleConstValue.EXECUTIVE_BOARD;

            // Xác định các key cache
            string generalCacheKey = RedisCacheKey.CONTRACT_CACHE_KEY;
            string userCacheKey = string.Format(RedisCacheKey.CONTRACT_BY_USER_CACHE_KEY, model.ActionBy);
            
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
                // Get the list of all projects if the user is an Executive Board member
                // Otherwise, get the list of projects the user has access to
                List<ProjectDTO> projects;
                if (isExecutiveBoard)
                {
                    // Executive Board members can see all projects
                    projects = await ListProject(new SearchProjectDTO()
                    {
                        ActionBy = model.ActionBy,
                        PageSize = int.MaxValue
                    });
                }
                else 
                {
                    // For regular users, get all projects they're associated with
                    projects = await ListProject(new SearchProjectDTO()
                    {
                        ActionBy = model.ActionBy,
                        PageSize = int.MaxValue
                    });
                }
                
                var projectIds = projects.Select(p => p.Id).ToList();
                
                // Lấy danh sách Contract thuộc các Project mà người dùng có quyền truy cập
                // Now with one-to-one relationship, each project has at most one contract
                var contracts = await _context.Contracts
                    .Where(t => !t.Deleted && projectIds.Contains(t.ProjectId))
                    .ToListAsync();
                
                // Lấy tất cả ContractDetail không bị xóa
                var allContractIds = contracts.Select(c => c.Id).ToList();
                var allContractDetails = await _context.Set<ContractDetail>()
                    .Where(cd => !cd.Deleted && allContractIds.Contains(cd.ContractId))
                    .ToListAsync();
                
                data = contracts.Select(t => new ContractDTO
                {
                    Id = t.Id,
                    ContractCode = t.ContractCode,
                    ContractName = t.ContractName,
                    Project = projects.FirstOrDefault(p => p.Id == t.ProjectId) ?? new ProjectDTO(),
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
                            Total = cd.Total,
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
            else if (!isExecutiveBoard)
            {
                // Nếu user không phải Executive Board, lọc theo quyền truy cập của người dùng
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
                                     || t.ContractName.ToLower().Trim().Contains(model.KeyWord.ToLower().Trim())
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
            var user = StaticVariable.UserMemory.FirstOrDefault(u => u.Id == model.ActionBy);
            
            // Check if user is in allowed roles (Business Employee or Executive Board)
            bool isInAllowedRole = _helpService.IsInRole(model.ActionBy, new List<string> { RoleConstValue.BUSINESS_EMPLOYEE, RoleConstValue.EXECUTIVE_BOARD });
            
            // If user is in allowed roles, allow access to all customers
            if (isInAllowedRole)
            {
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
            
            // For other users, check if they are associated with any projects
            // Get projects the user is associated with
            var projectUsers = await _context.ProjectUsers
                .Where(pu => pu.UserId == model.ActionBy && !pu.Deleted)
                .Select(pu => pu.ProjectId)
                .ToListAsync();
                
            if (!projectUsers.Any())
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            
            // Get projects with their customer info
            var projects = await _context.Projects
                .Where(p => projectUsers.Contains(p.Id) && !p.Deleted)
                .Select(p => p.CustomerId)
                .Distinct()
                .ToListAsync();
                
            // Get customers associated with those projects
            var customers = await _context.Customers
                .Where(c => projects.Contains(c.Id) && !c.Deleted)
                .OrderByDescending(t => t.UpdatedAt)
                .ToListAsync();
                
            if (!string.IsNullOrWhiteSpace(model.Search))
            {
                customers = customers.Where(t => t.CustomerName.ToLower().Trim().Contains(model.Search.ToLower().Trim())
                || t.CustomerCode.ToLower().Trim().Contains(model.Search.ToLower().Trim())
                || t.Phone.ToLower().Trim().Contains(model.Search.ToLower().Trim())).ToList();
            }
            
            model.Total = customers.Count();
            if (model.PageSize > 0)
            {
                customers = customers.Skip(model.Skip).Take(model.PageSize).ToList();
            }
            
            return customers;
        }

        public async Task<List<ProjectDTO>> ListProject(SearchProjectDTO model)
        {
            var user = StaticVariable.UserMemory.FirstOrDefault(u => u.Id == model.ActionBy);
            bool isExecutiveBoard = user != null && user.Role == RoleConstValue.EXECUTIVE_BOARD;

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
                
                if (isExecutiveBoard)
                {
                    foreach (var project in allProjects)
                    {
                        // Danh sách người có quyền xem project
                        var viewerIds = allProjectPermissions
                            .Where(pu => pu.ProjectId == project.Id && !pu.IsCreator && !pu.Deleted)
                            .Select(pu => pu.UserId)
                            .ToList();
                        
                        var customerEntity = allCustomers.FirstOrDefault(c => c.Id == project.CustomerId);
                        
                        // Tạo Customer mới với Projects = null để tránh vòng lặp tham chiếu
                        var customer = customerEntity != null ? 
                            new Customer {
                                Id = customerEntity.Id,
                                CustomerName = customerEntity.CustomerName,
                                DirectorName = customerEntity.DirectorName,
                                Phone = customerEntity.Phone,
                                Email = customerEntity.Email,
                                Address = customerEntity.Address,
                                Description = customerEntity.Description,
                                CustomerCode = customerEntity.CustomerCode,
                                TaxCode = customerEntity.TaxCode,
                                Fax = customerEntity.Fax,
                                BankAccount = customerEntity.BankAccount,
                                BankName = customerEntity.BankName,
                                CreatedAt = customerEntity.CreatedAt,
                                Creator = customerEntity.Creator,
                                UpdatedAt = customerEntity.UpdatedAt,
                                Updater = customerEntity.Updater,
                                Deleted = customerEntity.Deleted,
                                Projects = null // Ngăn tham chiếu vòng lặp
                            } : new Customer();
                        
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
                            IsCreator = false,
                            ViewerUserIds = viewerIds
                        });
                    }
                }
                else
                {
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
                            
                            var customerEntity = allCustomers.FirstOrDefault(c => c.Id == project.CustomerId);
                            
                            // Tạo Customer mới với Projects = null để tránh vòng lặp tham chiếu
                            var customer = customerEntity != null ? 
                                new Customer {
                                    Id = customerEntity.Id,
                                    CustomerName = customerEntity.CustomerName,
                                    DirectorName = customerEntity.DirectorName,
                                    Phone = customerEntity.Phone,
                                    Email = customerEntity.Email,
                                    Address = customerEntity.Address,
                                    Description = customerEntity.Description,
                                    CustomerCode = customerEntity.CustomerCode,
                                    TaxCode = customerEntity.TaxCode,
                                    Fax = customerEntity.Fax,
                                    BankAccount = customerEntity.BankAccount,
                                    BankName = customerEntity.BankName,
                                    CreatedAt = customerEntity.CreatedAt,
                                    Creator = customerEntity.Creator,
                                    UpdatedAt = customerEntity.UpdatedAt,
                                    Updater = customerEntity.Updater,
                                    Deleted = customerEntity.Deleted,
                                    Projects = null // Ngăn tham chiếu vòng lặp
                                } : new Customer();
                            
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
            
            var user = StaticVariable.UserMemory.FirstOrDefault(u => u.Id == model.ActionBy);
            bool isExecutiveBoard = user != null && user.Role == RoleConstValue.EXECUTIVE_BOARD;
            
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
            
            // Nếu không có trong cache chung, query từ database
            var data = await _context.SiteSurveys.Where(t => !t.Deleted).OrderByDescending(t => t.UpdatedAt).ToListAsync();
                
            // Si es Executive Board, devolver todos los sitios de estudio
            if (isExecutiveBoard)
            {
                // Cache todos los sitios para este usuario
                _ = _cacheService.SetAsync(userCacheKey, data, TimeSpan.FromMinutes(30));
                
                var filteredData = ApplyFilters(data, model);
                model.Total = filteredData.Count();
                
                if (model.PageSize > 0)
                {
                    filteredData = filteredData.Skip(model.Skip).Take(model.PageSize).ToList();
                }
                
                return filteredData;
            }
            
            // Lọc dữ liệu theo quyền truy cập của người dùng
            // Chỉ lấy các SiteSurvey thuộc project mà người dùng là thành viên
            
            // Lấy danh sách project mà người dùng có quyền truy cập
            var projectIds = await _context.ProjectUsers
                .Where(pu => pu.UserId == model.ActionBy && !pu.Deleted)
                .Select(pu => pu.ProjectId)
                .ToListAsync();
            
            // Lọc các SiteSurvey thuộc các project của người dùng
            var filteredSurveys = data.Where(s => projectIds.Contains(s.ProjectId)).ToList();
            
            // Cache cho user
            _ = _cacheService.SetAsync(userCacheKey, filteredSurveys, TimeSpan.FromMinutes(30));
            
            // Áp dụng bộ lọc
            var result = ApplyFilters(filteredSurveys, model);
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

            // Create a clean list without sensitive data
            var secureData = data.Select(user => new User {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role,
                FullName = user.FullName,
                Phone = user.Phone,
                Gender = user.Gender,
                Dob = user.Dob,
                IsVerify = user.IsVerify,
                TeamId = user.TeamId,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                Creator = user.Creator,
                Updater = user.Updater,
                Deleted = user.Deleted
                // RefreshTokens and PasswordHash fields are intentionally not included
            }).ToList();

            return secureData;
        }

        public async Task<List<Material>> ListMaterial(MaterialSearchDTO model)
        {
            if (!_helpService.IsInRole(model.ActionBy, RoleConstValue.RESOURCE_MANAGER))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Create cache key based on search parameters
            string cacheKey = GetMaterialSearchCacheKey(model);
            
            // Try to get from cache first
            var cachedMaterials = await _cacheService.GetAsync<List<Material>>(cacheKey);
            
            if (cachedMaterials != null)
            {
                _logger.LogInformation($"Cache hit for materials search: {cacheKey}");
                return cachedMaterials;
            }
            
            _logger.LogInformation($"Cache miss for materials search: {cacheKey}, fetching from database");

            var query = _context.Materials.Where(t => !t.Deleted).AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(model.MaterialCode))
            {
                query = query.Where(t => t.MaterialCode.ToLower().Contains(model.MaterialCode.ToLower()));
            }

            if (!string.IsNullOrEmpty(model.MaterialName))
            {
                query = query.Where(t => t.MaterialName.ToLower().Contains(model.MaterialName.ToLower()));
            }

            // Count total before pagination
            model.Total = await query.CountAsync();

            // Apply sorting and pagination
            var materials = await query
                .OrderByDescending(t => t.UpdatedAt)
                .Skip(model.Skip)
                .Take(model.PageSize)
                .ToListAsync();

            // Cache the result
            await _cacheService.SetAsync(cacheKey, materials, DEFAULT_CACHE_DURATION);
            
            return materials;
        }
        
        private string GetMaterialSearchCacheKey(MaterialSearchDTO model)
        {
            return $"{RedisCacheKey.MATERIAL_CACHE_KEY}_CODE_{model.MaterialCode ?? "all"}_NAME_{model.MaterialName ?? "all"}_PAGE_{model.PageIndex}_SIZE_{model.PageSize}";
        }

        public async Task<List<ConstructionTeam>> ListConstructionTeam(ConstructionTeamSearchDTO model)
        {
            // Check authorization - only Construction Manager, Technical Manager, and Executive Board can view teams
            if (!_helpService.IsInRole(model.ActionBy, new List<string> 
            { 
                RoleConstValue.CONSTRUCTION_MANAGER, 
                RoleConstValue.TECHNICAL_MANAGER, 
                RoleConstValue.EXECUTIVE_BOARD 
            }))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Try to get teams from cache
            string cacheKey = RedisCacheKey.CONSTRUCTION_TEAM_CACHE_KEY;
            var teamsCacheList = await _cacheService.GetAsync<List<ConstructionTeam>>(cacheKey);

            // If not in cache, get from database and cache it
            if (teamsCacheList == null)
            {
                teamsCacheList = await _context.ConstructionTeams
                    .Include(t => t.Manager)
                    .Include(t => t.Members)
                    .Where(t => !t.Deleted)
                    .OrderByDescending(t => t.UpdatedAt)
                    .ToListAsync();
                
                // Cache the result
                _ = _cacheService.SetAsync(cacheKey, teamsCacheList);
            }

            // Apply filters
            var filteredList = teamsCacheList;

            // Filter by team name
            if (!string.IsNullOrWhiteSpace(model.TeamName))
            {
                filteredList = filteredList.Where(t => 
                    t.TeamName.ToLower().Contains(model.TeamName.ToLower().Trim())
                ).ToList();
            }

            // Filter by team manager
            if (model.TeamManager.HasValue && model.TeamManager.Value > 0)
            {
                filteredList = filteredList.Where(t => t.TeamManager == model.TeamManager.Value).ToList();
            }

            // Set total count for pagination
            model.Total = filteredList.Count();

            // Apply pagination
            if (model.PageSize > 0)
            {
                filteredList = filteredList.Skip(model.Skip).Take(model.PageSize).ToList();
            }

            return filteredList;
        }

        public async Task<List<ConstructionPlanDTO>> ListConstructionPlan(ConstructionPlanQuery model)
        {
            // Check if user is authorized to perform this action
            if (!_helpService.IsInRole(model.ActionBy, new List<string> 
            { 
                RoleConstValue.CONSTRUCTION_MANAGER, 
                RoleConstValue.TECHNICAL_MANAGER, 
                RoleConstValue.RESOURCE_MANAGER,
                RoleConstValue.EXECUTIVE_BOARD 
            }))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            var user = StaticVariable.UserMemory.FirstOrDefault(u => u.Id == model.ActionBy);
            bool isExecutiveBoard = user != null && user.Role == RoleConstValue.EXECUTIVE_BOARD;

            // Define cache keys
            string generalCacheKey = RedisCacheKey.CONSTRUCTION_PLAN_CACHE_KEY;
            string userCacheKey = string.Format(RedisCacheKey.CONSTRUCTION_PLAN_BY_USER_CACHE_KEY, model.ActionBy);
            
            // Try to get from user-specific cache first
            var userCache = await _cacheService.GetAsync<List<ConstructionPlanDTO>>(userCacheKey);
            if (userCache != null)
            {
                var filteredData = ApplyConstructionPlanFilters(userCache, model);
                model.Total = filteredData.Count();
                
                if (model.PageSize > 0)
                {
                    int pageSize = model.PageSize == 0 ? 10 : model.PageSize;
                    int skip = (model.PageIndex - 1) * pageSize;
                    filteredData = filteredData.Skip(skip).Take(pageSize).ToList();
                }
                
                return filteredData;
            }

            // If not in user cache, try general cache
            var generalCache = await _cacheService.GetAsync<List<ConstructionPlanDTO>>(generalCacheKey);
            
            if (generalCache != null)
            {
                // Create user-specific cache with shorter expiration
                _ = _cacheService.SetAsync(userCacheKey, generalCache, TimeSpan.FromMinutes(30));
                
                var filteredData = ApplyConstructionPlanFilters(generalCache, model);
                model.Total = filteredData.Count();
                
                if (model.PageSize > 0)
                {
                    int pageSize = model.PageSize == 0 ? 10 : model.PageSize;
                    int skip = (model.PageIndex - 1) * pageSize;
                    filteredData = filteredData.Skip(skip).Take(pageSize).ToList();
                }
                
                return filteredData;
            }

            // If not in cache, get from database
            var constructionPlans = await _context.ConstructionPlans
                .Include(cp => cp.Project)
                .Include(cp => cp.Reviewers)
                .Where(cp => !cp.Deleted)
                .OrderByDescending(cp => cp.CreatedAt)
                .ToListAsync();

            // Convert to DTOs
            var constructionPlanDTOs = new List<ConstructionPlanDTO>();
            foreach (var plan in constructionPlans)
            {
                var creator = await _context.Users.FirstOrDefaultAsync(u => u.Id == plan.Creator);
                
                var dto = new ConstructionPlanDTO
                {
                    Id = plan.Id,
                    PlanName = plan.PlanName,
                    Reviewer = plan.Reviewer,
                    ProjectId = plan.ProjectId,
                    ProjectName = plan.Project?.ProjectName ?? "",
                    CreatedAt = plan.CreatedAt ?? DateTime.UtcNow,
                    UpdatedAt = plan.UpdatedAt ?? DateTime.UtcNow,
                    CreatedBy = plan.Creator,
                    CreatedByName = creator?.FullName ?? "",
                    UpdatedBy = plan.Updater,
                    IsApproved = plan.Reviewer != null && plan.Reviewer.Count > 0 && plan.Reviewer.All(r => r.Value == true)
                };
                
                constructionPlanDTOs.Add(dto);
            }

            // Cache the results
            _ = _cacheService.SetAsync(generalCacheKey, constructionPlanDTOs, TimeSpan.FromHours(1));
            _ = _cacheService.SetAsync(userCacheKey, constructionPlanDTOs, TimeSpan.FromMinutes(30));

            // Apply filters and pagination
            var result = ApplyConstructionPlanFilters(constructionPlanDTOs, model);
            model.Total = result.Count();
            
            if (model.PageSize > 0)
            {
                int pageSize = model.PageSize == 0 ? 10 : model.PageSize;
                int skip = (model.PageIndex - 1) * pageSize;
                result = result.Skip(skip).Take(pageSize).ToList();
            }
            
            return result;
        }
        
        private List<ConstructionPlanDTO> ApplyConstructionPlanFilters(List<ConstructionPlanDTO> data, ConstructionPlanQuery query)
        {
            var result = data;
            
            if (!string.IsNullOrEmpty(query.PlanName))
            {
                result = result.Where(cp => cp.PlanName.Contains(query.PlanName)).ToList();
            }

            if (query.ProjectId.HasValue)
            {
                result = result.Where(cp => cp.ProjectId == query.ProjectId.Value).ToList();
            }

            if (query.FromDate.HasValue)
            {
                var fromDate = query.FromDate.Value.Date;
                result = result.Where(cp => cp.CreatedAt >= fromDate).ToList();
            }

            if (query.ToDate.HasValue)
            {
                var toDate = query.ToDate.Value.Date.AddDays(1).AddTicks(-1);
                result = result.Where(cp => cp.CreatedAt <= toDate).ToList();
            }

            if (query.IsApproved.HasValue)
            {
                if (query.IsApproved.Value)
                {
                    result = result.Where(cp => cp.IsApproved).ToList();
                }
                else
                {
                    result = result.Where(cp => !cp.IsApproved).ToList();
                }
            }
            
            return result;
        }
    }
}
