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

            // Use only the main cache key
            string cacheKey = RedisCacheKey.CONTRACT_CACHE_KEY;
            
            // Try to get from main cache
            var allContracts = await _cacheService.GetAsync<List<ContractDTO>>(cacheKey);
            
            if (allContracts != null)
            {
                // If user is not Executive Board, filter contracts by user's project access
                var filteredContracts = allContracts;
                
                if (!isExecutiveBoard)
                {
                    // Get projects the user has access to
                    var userProjectIds = await _context.ProjectUsers
                        .Where(pu => pu.UserId == model.ActionBy && !pu.Deleted)
                        .Select(pu => pu.ProjectId)
                        .ToListAsync();
                        
                    // Filter contracts by those projects
                    filteredContracts = filteredContracts.Where(c => 
                        c.Project != null && userProjectIds.Contains(c.Project.Id)).ToList();
                }
                
                // Apply search filters
                var filteredResult = ApplyContractFilters(filteredContracts, model);
                
                // Set total count for pagination
                model.Total = filteredResult.Count();
                
                // Apply pagination if needed
                if (model.PageSize > 0)
                {
                    filteredResult = filteredResult.Skip(model.Skip).Take(model.PageSize).ToList();
                }
                
                return filteredResult;
            }

            // If not in cache, get from database
            
            // Get the list of projects the user has access to
            List<ProjectDTO> projects = await ListProject(new SearchProjectDTO()
            {
                ActionBy = model.ActionBy,
                PageSize = int.MaxValue
            });
            
            var projectIds = projects.Select(p => p.Id).ToList();
            
            // Get contracts for those projects
            var contracts = await _context.Contracts
                .Where(t => !t.Deleted && projectIds.Contains(t.ProjectId))
                .OrderByDescending(t => t.UpdatedAt)
                .ToListAsync();
            
            // Get all contract details for these contracts
            var allContractIds = contracts.Select(c => c.Id).ToList();
            var allContractDetails = await _context.Set<ContractDetail>()
                .Where(cd => !cd.Deleted && allContractIds.Contains(cd.ContractId))
                .ToListAsync();
            
            // Map to DTOs
            var contractDTOs = contracts.Select(t => new ContractDTO
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
                // Add ContractDetails for each Contract
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
            }).ToList();

            // Save all contracts to main cache
            await _cacheService.SetAsync(cacheKey, contractDTOs, TimeSpan.FromHours(1));
            
            // Apply search filters
            var result = ApplyContractFilters(contractDTOs, model);
            
            // Set total count for pagination
            model.Total = result.Count();
            
            // Apply pagination if needed
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

            // Use the main cache key
            string projectCacheKey = RedisCacheKey.PROJECT_CACHE_KEY;
            
            // Get projects from the main cache
            var allProjects = await _cacheService.GetAsync<List<ProjectDTO>>(projectCacheKey);
            
            if (allProjects != null)
            {
                // Get the current user's project associations
                var userProjectAssociations = await _context.ProjectUsers
                    .Where(pu => pu.UserId == model.ActionBy && !pu.Deleted)
                    .ToListAsync();
                
                // Update IsCreator flag and ViewerUserIds for the current user
                foreach (var project in allProjects)
                {
                    // Check if this user is the creator or viewer in ProjectUsers table
                    var projectUserRecord = userProjectAssociations.FirstOrDefault(pu => pu.ProjectId == project.Id);
                    
                    // Update IsCreator flag if user is associated with this project
                    project.IsCreator = projectUserRecord != null && projectUserRecord.IsCreator;
                    
                    // Ensure the current user is in the ViewerUserIds list if they're associated with this project
                    if (projectUserRecord != null && !projectUserRecord.IsCreator && 
                        project.ViewerUserIds != null && !project.ViewerUserIds.Contains(model.ActionBy))
                    {
                        project.ViewerUserIds.Add(model.ActionBy);
                    }
                }
                
                // Filter projects based on user's access rights
                var filteredProjects = FilterProjectsByUserAccess(allProjects, model.ActionBy, isExecutiveBoard);
                
                // Apply search filters
                if (!string.IsNullOrEmpty(model.KeyWord))
                {
                    filteredProjects = filteredProjects.Where(p => 
                        p.ProjectName.Contains(model.KeyWord) || 
                        p.ProjectCode.Contains(model.KeyWord) || 
                        (p.Customer != null && p.Customer.CustomerName.Contains(model.KeyWord))
                    ).ToList();
                }
                
                if (model.CustomerId > 0)
                {
                    filteredProjects = filteredProjects.Where(p => p.Customer?.Id == model.CustomerId).ToList();
                }
                
                if (model.Status.HasValue)
                {
                    filteredProjects = filteredProjects.Where(p => p.Status == model.Status.Value).ToList();
                }
                
                // Apply pagination
                model.Total = filteredProjects.Count;
                if (model.PageSize > 0)
                {
                    filteredProjects = filteredProjects
                        .Skip(model.Skip)
                        .Take(model.PageSize)
                        .ToList();
                }
                
                return filteredProjects;
            }
            
            // If not in cache, load from database and build DTOs
            
            // 1. Get all projects
            var projectEntities = await _context.Projects
                .Where(t => !t.Deleted)
                .OrderByDescending(t => t.UpdatedAt)
                .ToListAsync();
            
            // 2. Get project permissions
            var projectUserEntities = await _context.ProjectUsers
                .Where(pu => !pu.Deleted)
                .ToListAsync();
            
            // 3. Get customer information
            var allCustomers = await _context.Customers
                .Where(c => !c.Deleted)
                .ToListAsync();
            
            // 4. Build DTOs for all projects
            var projectDTOs = new List<ProjectDTO>();
            
            foreach (var project in projectEntities)
            {
                // Get viewers for this project
                var viewerIds = projectUserEntities
                    .Where(pu => pu.ProjectId == project.Id && !pu.IsCreator && !pu.Deleted)
                    .Select(pu => pu.UserId)
                    .ToList();
                
                // Get creator information
                var isCreator = projectUserEntities.Any(pu => 
                    pu.ProjectId == project.Id && 
                    pu.IsCreator && 
                    pu.UserId == model.ActionBy);
                
                // Get customer information
                var customerEntity = allCustomers.FirstOrDefault(c => c.Id == project.CustomerId);
                
                // Create customer object with null Projects property to avoid circular references
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
                        Projects = null // Prevent circular references
                    } : new Customer();
                
                // Create project DTO
                projectDTOs.Add(new ProjectDTO
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
            
            // Cache all projects
            await _cacheService.SetAsync(projectCacheKey, projectDTOs, TimeSpan.FromHours(1));
            
            // Filter projects by user access
            var userProjects = FilterProjectsByUserAccess(projectDTOs, model.ActionBy, isExecutiveBoard);
            
            // Apply search filters
            if (!string.IsNullOrEmpty(model.KeyWord))
            {
                userProjects = userProjects.Where(p => 
                    p.ProjectName.Contains(model.KeyWord) || 
                    p.ProjectCode.Contains(model.KeyWord) || 
                    (p.Customer != null && p.Customer.CustomerName.Contains(model.KeyWord))
                ).ToList();
            }
            
            if (model.CustomerId > 0)
            {
                userProjects = userProjects.Where(p => p.Customer?.Id == model.CustomerId).ToList();
            }
            
            if (model.Status.HasValue)
            {
                userProjects = userProjects.Where(p => p.Status == model.Status.Value).ToList();
            }
            
            // Apply pagination
            model.Total = userProjects.Count;
            if (model.PageSize > 0)
            {
                userProjects = userProjects
                    .Skip(model.Skip)
                    .Take(model.PageSize)
                    .ToList();
            }
            
            return userProjects;
        }

        // Helper method to filter projects based on user access
        private List<ProjectDTO> FilterProjectsByUserAccess(List<ProjectDTO> projects, int userId, bool isExecutiveBoard)
        {
            // If user is Executive Board, they can see all projects
            if (isExecutiveBoard)
            {
                return projects;
            }
            
            // Otherwise, filter projects by user access
            return projects.Where(p => 
                // User is creator - checking either the Creator field OR the IsCreator flag
                p.Creator == userId || p.IsCreator || 
                // User is in viewers list
                (p.ViewerUserIds != null && p.ViewerUserIds.Contains(userId))
            ).ToList();
        }

        public async Task<List<SiteSurvey>> ListSiteSurvey(SearchSiteSurveyDTO model)
        {
            // Check if user is authorized to perform this action
            if (!_helpService.IsInRole(model.ActionBy, new List<string> 
            { 
                RoleConstValue.TECHNICAL_MANAGER, 
                RoleConstValue.EXECUTIVE_BOARD 
            }))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            var user = StaticVariable.UserMemory.FirstOrDefault(u => u.Id == model.ActionBy);
            bool isExecutiveBoard = user != null && user.Role == RoleConstValue.EXECUTIVE_BOARD;

            // Only use the main cache key
            string cacheKey = RedisCacheKey.SITE_SURVEY_CACHE_KEY;
            
            // Try to get from cache
            var cachedData = await _cacheService.GetAsync<List<SiteSurvey>>(cacheKey);
            
            if (cachedData != null)
            {
                // Apply filters to cached data
                var filteredData = ApplyFilters(cachedData, model);
                
                // If not Executive Board, filter by user's project access
                if (!isExecutiveBoard)
                {
                    var userProjects = await _context.ProjectUsers
                        .Where(pu => pu.UserId == model.ActionBy && !pu.Deleted)
                        .Select(pu => pu.ProjectId)
                        .ToListAsync();
                        
                    filteredData = filteredData.Where(s => userProjects.Contains(s.ProjectId)).ToList();
                }
                
                // Set total count for pagination
                model.Total = filteredData.Count();
                
                // Apply pagination if needed
                if (model.PageSize > 0)
                {
                    int skip = (model.PageIndex - 1) * model.PageSize;
                    filteredData = filteredData.Skip(skip).Take(model.PageSize).ToList();
                }
                
                return filteredData;
            }

            // If not in cache, get from database
            var query = _context.SiteSurveys.Where(s => !s.Deleted);
            
            // If not Executive Board, filter by user's project access
            if (!isExecutiveBoard)
            {
                var userProjects = await _context.ProjectUsers
                    .Where(pu => pu.UserId == model.ActionBy && !pu.Deleted)
                    .Select(pu => pu.ProjectId)
                    .ToListAsync();
                    
                query = query.Where(s => userProjects.Contains(s.ProjectId));
            }
            
            // Execute query and get all site surveys
            var siteSurveys = await query.ToListAsync();

            // Cache all site surveys for future use
            await _cacheService.SetAsync(cacheKey, siteSurveys, TimeSpan.FromHours(1));

            // Apply filters and pagination for this specific request
            var result = ApplyFilters(siteSurveys, model);
            model.Total = result.Count();
            
            if (model.PageSize > 0)
            {
                int skip = (model.PageIndex - 1) * model.PageSize;
                result = result.Skip(skip).Take(model.PageSize).ToList();
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

        public Task<List<User>> ListUser(AdminSearchUserDTO model)
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

            return Task.FromResult(secureData);
        }

        public async Task<List<Material>> ListMaterial(MaterialSearchDTO model)
        {
            // Remove role-based authorization check - all authenticated users can view materials

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
            // Remove role-based authorization check - all authenticated users can view teams
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
            // Verify access rights
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

            // Only use the main cache key
            string cacheKey = RedisCacheKey.CONSTRUCTION_PLAN_CACHE_KEY;
            
            // Try to get data from cache
            var cachedData = await _cacheService.GetAsync<List<ConstructionPlanDTO>>(cacheKey);
            
            if (cachedData != null)
            {
                // Apply filters to the cached data
                var filteredData = ApplyConstructionPlanFilters(cachedData, model);
                
                // Filter by user access if not Executive Board
                if (!isExecutiveBoard)
                {
                    // Get projects the user has access to
                    var userProjects = await _context.ProjectUsers
                        .Where(pu => pu.UserId == model.ActionBy && !pu.Deleted)
                        .Select(pu => pu.ProjectId)
                        .ToListAsync();
                        
                    // Filter construction plans by those projects
                    filteredData = filteredData.Where(cp => userProjects.Contains(cp.ProjectId)).ToList();
                }
                
                // Set total count for pagination
                model.Total = filteredData.Count();
                
                // Apply pagination if requested
                if (model.PageSize > 0)
                {
                    int pageSize = model.PageSize == 0 ? 10 : model.PageSize;
                    int skip = (model.PageIndex - 1) * pageSize;
                    filteredData = filteredData.Skip(skip).Take(pageSize).ToList();
                }
                
                return filteredData;
            }

            // If not in cache, get from database
            var query = _context.ConstructionPlans
                .Include(cp => cp.Project)
                .Include(cp => cp.Reviewers)
                .Where(cp => !cp.Deleted);
                
            // Apply user access filter to database query if not Executive Board
            if (!isExecutiveBoard)
            {
                var userProjects = await _context.ProjectUsers
                    .Where(pu => pu.UserId == model.ActionBy && !pu.Deleted)
                    .Select(pu => pu.ProjectId)
                    .ToListAsync();
                    
                query = query.Where(cp => userProjects.Contains(cp.ProjectId));
            }
            
            // Execute query and get all plans
            var constructionPlans = await query
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

            // Cache all construction plans (not filtered) for future use
            await _cacheService.SetAsync(cacheKey, constructionPlanDTOs, TimeSpan.FromHours(1));

            // Apply filters and pagination for this specific request
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
