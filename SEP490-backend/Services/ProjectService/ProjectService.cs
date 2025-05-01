using Microsoft.EntityFrameworkCore;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.Project;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Infra.Enums;
using Sep490_Backend.Services.CacheService;
using Sep490_Backend.Services.DataService;
using Sep490_Backend.Services.HelperService;
using Sep490_Backend.Services.GoogleDriveService;
using Sep490_Backend.Services.MaterialService;
using Sep490_Backend.Infra.Helps;
using Sep490_Backend.Controllers;
using System.Text.Json;
using Sep490_Backend.DTO;
using Microsoft.Extensions.Logging;

namespace Sep490_Backend.Services.ProjectService
{
    public interface IProjectService
    {
        Task<ProjectDTO> Save(SaveProjectDTO model, int actionBy);
        Task<int> Delete(int id, int actionBy);
        Task<ListProjectStatusDTO> ListProjectStatus(int actionBy);
        Task<ProjectDTO> Detail(int id, int actionBy);
        Task<bool> UpdateStatus(UpdateProjectStatusDTO model, int actionBy);
    }

    public class ProjectService : IProjectService
    {
        private readonly BackendContext _context;
        private readonly ICacheService _cacheService;
        private readonly IHelperService _helperService;
        private readonly IDataService _dataService;
        private readonly IGoogleDriveService _googleDriveService;
        private readonly IMaterialService _materialService;
        private readonly ILogger<ProjectService> _logger;

        public ProjectService(
            BackendContext context, 
            IDataService dataService, 
            ICacheService cacheService, 
            IHelperService helperService,
            IGoogleDriveService googleDriveService,
            IMaterialService materialService,
            ILogger<ProjectService> logger)
        {
            _context = context;
            _cacheService = cacheService;
            _helperService = helperService;
            _dataService = dataService;
            _googleDriveService = googleDriveService;
            _materialService = materialService;
            _logger = logger;
        } 

        public async Task<int> Delete(int id, int actionBy)
        {
            if (!_helperService.IsInRole(actionBy, RoleConstValue.BUSINESS_EMPLOYEE))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Get project with related entities for cascade delete
            var entity = await _context.Projects
                .IncludeRelatedEntities()
                .FirstOrDefaultAsync(t => t.Id == id);
            
            if (entity == null)
            {
                throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
            }
            
            var user = StaticVariable.UserMemory.FirstOrDefault(u => u.Id == actionBy);
            if (user != null && user.Role == RoleConstValue.EXECUTIVE_BOARD)
            {
                // Si es Executive Board, permiso sin restricciones
            }
            else
            {
                // Kiểm tra xem người gọi có phải là người tạo project không
                var projectCreator = await _context.ProjectUsers
                    .FirstOrDefaultAsync(pu => pu.ProjectId == id && pu.IsCreator && !pu.Deleted);
                    
                if (projectCreator == null || projectCreator.UserId != actionBy)
                {
                    throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
                }
            }

            // Use the extension method for soft delete
            await _context.SoftDeleteAsync(entity, actionBy);
            
            // Clear all relevant caches
            await InvalidateProjectRelatedCaches(id);
            
            return entity.Id;
        }

        // Helper method to invalidate all project-related caches
        private async Task InvalidateProjectRelatedCaches(int projectId)
        {
            // Specific project cache keys
            var specificCacheKeys = new List<string>
            {
                string.Format(RedisCacheKey.PROJECT_BY_ID_CACHE_KEY, projectId)
            };
            
            // Main cache keys to be invalidated
            var mainCacheKeys = new[]
            {
                RedisCacheKey.PROJECT_CACHE_KEY,
                RedisCacheKey.PROJECT_USER_CACHE_KEY,
                RedisCacheKey.PROJECT_LIST_CACHE_KEY,
                RedisCacheKey.SITE_SURVEY_CACHE_KEY,
                RedisCacheKey.CONTRACT_CACHE_KEY,
                RedisCacheKey.CONTRACT_DETAIL_CACHE_KEY,
                RedisCacheKey.CONSTRUCTION_PLAN_CACHE_KEY,
                RedisCacheKey.RESOURCE_MOBILIZATION_REQ_CACHE_KEY
            };
            
            // Delete specific project cache keys
            foreach (var cacheKey in specificCacheKeys)
            {
                await _cacheService.DeleteAsync(cacheKey);
            }
            
            // Delete all main cache keys
            foreach (var cacheKey in mainCacheKeys)
            {
                await _cacheService.DeleteAsync(cacheKey);
            }

            // Delete pattern-based caches
            await _cacheService.DeleteByPatternAsync(RedisCacheKey.PROJECT_ALL_PATTERN);
            await _cacheService.DeleteByPatternAsync(RedisCacheKey.CONTRACT_ALL_PATTERN);
            await _cacheService.DeleteByPatternAsync(RedisCacheKey.MOBILIZATION_REQ_CACHE_KEY);
            await _cacheService.DeleteByPatternAsync(RedisCacheKey.MOBILIZATION_REQS_LIST_CACHE_KEY);
            await _cacheService.DeleteByPatternAsync(string.Format(RedisCacheKey.MOBILIZATION_REQ_BY_PROJECT_CACHE_KEY, projectId));
            await _cacheService.DeleteByPatternAsync(string.Format(RedisCacheKey.MOBILIZATION_REQS_BY_PROJECT_LIST_CACHE_KEY, projectId));
        }

        public async Task<ProjectDTO> Detail(int id, int actionBy)
        {
            // First try to get from project-specific cache
            string projectCacheKey = string.Format(RedisCacheKey.PROJECT_BY_ID_CACHE_KEY, id);
            var projectFromCache = await _cacheService.GetAsync<ProjectDTO>(projectCacheKey);
            
            if (projectFromCache != null)
            {
                // Verify user has access to this project
                if (CanUserAccessProject(projectFromCache, actionBy))
                {
                    return projectFromCache;
                }
                else
                {
                    throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
                }
            }
            
            // If not in specific cache, check the main projects cache
            var allProjects = await _cacheService.GetAsync<List<ProjectDTO>>(RedisCacheKey.PROJECT_CACHE_KEY);
            
            if (allProjects != null)
            {
                // Filter from main cache by id
                var projectFromMainCache = allProjects.FirstOrDefault(p => p.Id == id);
                if (projectFromMainCache != null)
                {
                    // Check if user has access
                    if (CanUserAccessProject(projectFromMainCache, actionBy))
                    {
                        // Save to specific cache for faster retrieval next time
                        await _cacheService.SetAsync(projectCacheKey, projectFromMainCache, TimeSpan.FromHours(1));
                        return projectFromMainCache;
                    }
                    else
                    {
                        throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
                    }
                }
            }
            
            // If not in cache, query from database
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == id && !p.Deleted);
            if (project == null)
            {
                throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
            }
            
            // Check if user has permission to view the project
            if (!await CheckProjectAccessAsync(project.Id, actionBy))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            
            // Get project details by mapping to DTO
            var projectDTO = await MapProjectToDTO(project, actionBy);
            
            // Cache the retrieved project
            await _cacheService.SetAsync(projectCacheKey, projectDTO, TimeSpan.FromHours(1));
            
            // Update main cache if needed
            await UpdateProjectInMainCache(projectDTO);
            
            return projectDTO;
        }
        
        // Helper method to update the project in main cache
        private async Task UpdateProjectInMainCache(ProjectDTO projectDTO)
        {
            var allProjects = await _cacheService.GetAsync<List<ProjectDTO>>(RedisCacheKey.PROJECT_CACHE_KEY);
            
            if (allProjects == null)
            {
                // If main cache is empty, load all projects
                allProjects = await GetAllProjectDTOs();
                await _cacheService.SetAsync(RedisCacheKey.PROJECT_CACHE_KEY, allProjects, TimeSpan.FromHours(1));
                await _cacheService.SetAsync(RedisCacheKey.PROJECT_LIST_CACHE_KEY, allProjects, TimeSpan.FromHours(1));
            }
            else
            {
                // Find and update the project in cache
                var existingProject = allProjects.FirstOrDefault(p => p.Id == projectDTO.Id);
                if (existingProject != null)
                {
                    // Update existing project
                    int index = allProjects.IndexOf(existingProject);
                    allProjects[index] = projectDTO;
                }
                else
                {
                    // Add new project to cache
                    allProjects.Add(projectDTO);
                }
                
                // Update both cache keys
                await _cacheService.SetAsync(RedisCacheKey.PROJECT_CACHE_KEY, allProjects, TimeSpan.FromHours(1));
                await _cacheService.SetAsync(RedisCacheKey.PROJECT_LIST_CACHE_KEY, allProjects, TimeSpan.FromHours(1));
            }
        }
        
        // Helper method to check if user has access to a project
        private bool CanUserAccessProject(ProjectDTO project, int userId)
        {
            var user = StaticVariable.UserMemory.FirstOrDefault(u => u.Id == userId);
            
            // Executive Board has access to all projects
            if (user != null && user.Role == RoleConstValue.EXECUTIVE_BOARD)
            {
                return true;
            }
            
            // Check if user is the creator of the project
            if (project.Creator == userId || project.IsCreator)
            {
                return true;
            }
            
            // Check if user is in ViewerUserIds list
            if (project.ViewerUserIds != null && project.ViewerUserIds.Contains(userId))
            {
                return true;
            }
            
            // Check if user is a member of the project through ProjectUsers
            return project.ProjectUsers != null && 
                  project.ProjectUsers.Any(pu => pu.UserId == userId && !pu.Deleted);
        }
        
        // Helper method to check project access from database
        private async Task<bool> CheckProjectAccessAsync(int projectId, int userId)
        {
            var user = StaticVariable.UserMemory.FirstOrDefault(u => u.Id == userId);
            
            // Executive Board has access to all projects
            if (user != null && user.Role == RoleConstValue.EXECUTIVE_BOARD)
            {
                return true;
            }
            
            // Otherwise, check if user is a creator or viewer of the project
            return await _context.ProjectUsers
                .AnyAsync(pu => pu.ProjectId == projectId && pu.UserId == userId && !pu.Deleted);
        }
        
        // Helper method to get all projects as DTOs
        private async Task<List<ProjectDTO>> GetAllProjectDTOs()
        {
            var projects = await _context.Projects
                .Include(p => p.ProjectUsers)
                .Where(p => !p.Deleted)
                .ToListAsync();
                
            List<ProjectDTO> projectDTOs = new List<ProjectDTO>();
            
            foreach (var project in projects)
            {
                var dto = await MapProjectToDTO(project, -1); // Using -1 as a marker for admin-level access
                projectDTOs.Add(dto);
            }
            
            return projectDTOs;
        }
        
        // Assume this is a method to map a project entity to a DTO
        private async Task<ProjectDTO> MapProjectToDTO(Project project, int actionBy)
        {
                // Get the project users including viewers (users who aren't creators)
                var projectUsers = await _context.ProjectUsers
                    .Where(pu => pu.ProjectId == project.Id && !pu.Deleted)
                    .ToListAsync();
                    
                // Get viewer user IDs
                var viewerUserIds = projectUsers
                    .Where(pu => !pu.IsCreator)
                    .Select(pu => pu.UserId)
                    .ToList();
                    
                // Check if this user is the creator
                bool isCreator = projectUsers.Any(pu => pu.UserId == actionBy && pu.IsCreator);
                
                // Get customer information
                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == project.CustomerId);
                
            return new ProjectDTO
                {
                    Id = project.Id,
                    ProjectCode = project.ProjectCode,
                    ProjectName = project.ProjectName,
                    Customer = customer != null ? new Customer
                    {
                        Id = customer.Id,
                        CustomerName = customer.CustomerName,
                        DirectorName = customer.DirectorName,
                        Phone = customer.Phone,
                        Email = customer.Email,
                        Address = customer.Address,
                        Description = customer.Description,
                        CustomerCode = customer.CustomerCode,
                        TaxCode = customer.TaxCode,
                        Fax = customer.Fax,
                        BankAccount = customer.BankAccount,
                        BankName = customer.BankName,
                        CreatedAt = customer.CreatedAt,
                        Creator = customer.Creator,
                        UpdatedAt = customer.UpdatedAt,
                        Updater = customer.Updater,
                        Deleted = customer.Deleted,
                        Projects = null // Prevent circular references
                    } : new Customer(),
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
                    ViewerUserIds = viewerUserIds,
                    ProjectUsers = projectUsers.Select(pu => new DTO.Project.ProjectUserDTO
                    {
                        Id = pu.Id,
                        UserId = pu.UserId,
                        ProjectId = pu.ProjectId,
                        IsCreator = pu.IsCreator
                    }).ToList()
                };
        }

        public async Task<ListProjectStatusDTO> ListProjectStatus(int actionBy)
        {
            var data = await _dataService.ListProject(new SearchProjectDTO()
            {
                ActionBy = actionBy,
                PageSize = int.MaxValue
            });
            var result = new ListProjectStatusDTO
            {
                ReceiveRequest = data.Where(t => t.Status == Infra.Enums.ProjectStatusEnum.ReceiveRequest).Count(),
                Planning = data.Where(t => t.Status == Infra.Enums.ProjectStatusEnum.Planning).Count(),
                InProgress = data.Where(t => t.Status == Infra.Enums.ProjectStatusEnum.InProgress).Count(),
                Completed = data.Where(t => t.Status == Infra.Enums.ProjectStatusEnum.Completed).Count(),
                Paused = data.Where(t => t.Status == Infra.Enums.ProjectStatusEnum.Paused).Count(),
                Closed = data.Where(t => t.Status == Infra.Enums.ProjectStatusEnum.Closed).Count(),
                Total = data.Count()
            };

            return result;
        }

        public async Task<ProjectDTO> Save(SaveProjectDTO model, int actionBy)
        {
            if (!_helperService.IsInRole(actionBy, new List<string>{ RoleConstValue.BUSINESS_EMPLOYEE, RoleConstValue.EXECUTIVE_BOARD }))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            var errors = new List<ResponseError>();

            var customer = await _dataService.ListCustomer(new DTO.Customer.CustomerSearchDTO
            {
                ActionBy = actionBy,
                PageSize = int.MaxValue
            });
            var data = _context.Projects.Where(t => !t.Deleted).ToList();

            if (model.StartDate > model.EndDate)
            {
                errors.Add(new ResponseError
                {
                    Message = Message.ProjectMessage.INVALID_DATE,
                    Field = nameof(model.StartDate).ToCamelCase() + ", " + nameof(model.EndDate).ToCamelCase()
                });
            }
            if (customer.FirstOrDefault(t => t.Id == model.CustomerId) == null)
            {
                errors.Add(new ResponseError
                {
                    Message = Message.CustomerMessage.CUSTOMER_NOT_FOUND,
                    Field = nameof(model.CustomerId).ToCamelCase()
                });
            }

            if (errors.Count > 0)
                throw new ValidationException(errors);

            // Handle file attachments
            List<AttachmentInfo> attachmentInfos = new List<AttachmentInfo>();
            string existingAttachmentsJson = null;

            if (model.Id != 0)
            {
                // If this is an update, get the existing project to check old attachments
                var existingProject = await _context.Projects.FirstOrDefaultAsync(t => t.Id == model.Id);
                if (existingProject?.Attachments != null)
                {
                    existingAttachmentsJson = existingProject.Attachments.RootElement.ToString();
                    attachmentInfos = System.Text.Json.JsonSerializer.Deserialize<List<AttachmentInfo>>(existingAttachmentsJson);
                }
                
                // Verificar si el usuario es Executive Board
                var user = StaticVariable.UserMemory.FirstOrDefault(u => u.Id == actionBy);
                if (user != null && user.Role == RoleConstValue.EXECUTIVE_BOARD)
                {
                    // Si es Executive Board, permiso sin restricciones
                }
                else
                {
                    // Kiểm tra quyền truy cập - chỉ người tạo mới có quyền chỉnh sửa
                    var projectCreator = await _context.ProjectUsers
                        .FirstOrDefaultAsync(pu => pu.ProjectId == model.Id && pu.IsCreator && !pu.Deleted);
                    
                    if (projectCreator == null || projectCreator.UserId != actionBy)
                    {
                        throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
                    }
                }
            }

            if (model.Attachments != null && model.Attachments.Any())
            {
                // If there are existing attachments and we're uploading new ones, delete the old ones
                if (attachmentInfos.Any())
                {
                    try
                    {
                        var linksToDelete = attachmentInfos.Select(a => a.WebContentLink).ToList();
                        await _googleDriveService.DeleteFilesByLinks(linksToDelete);
                        attachmentInfos.Clear();
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue with upload
                        Console.WriteLine($"Failed to delete old attachments: {ex.Message}");
                    }
                }

                // Upload new files
                foreach (var file in model.Attachments)
                {
                    using (var stream = file.OpenReadStream())
                    {
                        var uploadResult = await _googleDriveService.UploadFile(
                            stream,
                            file.FileName,
                            file.ContentType
                        );

                        // Parse Google Drive response to get file ID
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

            var newProject = new Project
            {
                ProjectCode = model.ProjectCode,
                ProjectName = model.ProjectName,
                CustomerId = model.CustomerId,
                ConstructType = model.ConstructType,
                Location = model.Location,
                Area = model.Area,
                Purpose = model.Purpose,
                TechnicalReqs = model.TechnicalReqs,
                StartDate = model.StartDate,
                EndDate = model.EndDate,
                Budget = model.Budget,
                Status = ProjectStatusEnum.ReceiveRequest, // Set default status for new projects
                Description = model.Description,
                Creator = actionBy,
                Updater = actionBy,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            if (model.Id != 0)
            {
                var entity = await _context.Projects.FirstOrDefaultAsync(t => t.Id == model.Id);
                if (entity == null)
                {
                    throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
                }
                if (data.FirstOrDefault(t => t.ProjectCode == model.ProjectCode && t.ProjectCode != entity.ProjectCode) != null)
                {
                    errors.Add(new ResponseError
                    {
                        Message = Message.ProjectMessage.PROJECT_CODE_EXIST,
                        Field = nameof(model.ProjectCode).ToCamelCase()
                    });
                    if (errors.Count > 0)
                        throw new ValidationException(errors);
                }

                // Update existing entity properties
                entity.ProjectCode = model.ProjectCode;
                entity.ProjectName = model.ProjectName;
                entity.CustomerId = model.CustomerId;
                entity.ConstructType = model.ConstructType;
                entity.Location = model.Location;
                entity.Area = model.Area;
                entity.Purpose = model.Purpose;
                entity.TechnicalReqs = model.TechnicalReqs;
                entity.StartDate = model.StartDate;
                entity.EndDate = model.EndDate;
                entity.Budget = model.Budget;
                entity.Description = model.Description;
                entity.Updater = actionBy;
                entity.UpdatedAt = DateTime.Now;

                _context.Update(entity);
                newProject = entity; // Use the updated entity for the return value
                
                // Xóa mềm danh sách người xem hiện tại
                var currentViewers = await _context.ProjectUsers
                    .Where(pu => pu.ProjectId == model.Id && !pu.IsCreator && !pu.Deleted)
                    .ToListAsync();
                
                foreach (var viewer in currentViewers)
                {
                    viewer.Deleted = true;
                    viewer.UpdatedAt = DateTime.UtcNow;
                    viewer.Updater = actionBy;
                    _context.Update(viewer);
                }
            }
            else
            {
                if (data.FirstOrDefault(t => t.ProjectCode == model.ProjectCode) != null)
                {
                    errors.Add(new ResponseError
                    {
                        Message = Message.ProjectMessage.PROJECT_CODE_EXIST,
                        Field = nameof(model.ProjectCode).ToCamelCase()
                    });
                    if (errors.Count > 0)
                        throw new ValidationException(errors);
                }

                await _context.AddAsync(newProject);
                await _context.SaveChangesAsync(); // Lưu project trước để có Id
                
                // Thêm mới bản ghi người tạo
                var creatorRecord = new ProjectUser
                {
                    ProjectId = newProject.Id,
                    UserId = actionBy,
                    IsCreator = true,
                    CreatedAt = DateTime.UtcNow,
                    Creator = actionBy,
                    UpdatedAt = DateTime.UtcNow,
                    Updater = actionBy,
                    Deleted = false
                };
                
                await _context.AddAsync(creatorRecord);
            }
            
            // Thêm mới danh sách người được phép xem
            foreach (var viewerId in model.ViewerUserIds.Distinct())
            {
                // Bỏ qua nếu là người tạo
                if (viewerId == actionBy)
                    continue;
                    
                var viewerRecord = new ProjectUser
                {
                    ProjectId = newProject.Id,
                    UserId = viewerId,
                    IsCreator = false,
                    CreatedAt = DateTime.UtcNow,
                    Creator = actionBy,
                    UpdatedAt = DateTime.UtcNow,
                    Updater = actionBy,
                    Deleted = false
                };
                
                await _context.AddAsync(viewerRecord);
            }

            await _context.SaveChangesAsync();
            
            // Invalidate all project-related caches
            await InvalidateProjectRelatedCaches(newProject.Id);
            
            // Map to DTO for return value
            var projectDTO = new ProjectDTO
            {
                Id = newProject.Id,
                ProjectCode = newProject.ProjectCode,
                ProjectName = newProject.ProjectName,
                // Tạo Customer mới với Projects = null để tránh vòng lặp tham chiếu
                Customer = customer.FirstOrDefault(c => c.Id == newProject.CustomerId) != null ? new Customer
                {
                    Id = customer.FirstOrDefault(c => c.Id == newProject.CustomerId).Id,
                    CustomerName = customer.FirstOrDefault(c => c.Id == newProject.CustomerId).CustomerName,
                    DirectorName = customer.FirstOrDefault(c => c.Id == newProject.CustomerId).DirectorName,
                    Phone = customer.FirstOrDefault(c => c.Id == newProject.CustomerId).Phone,
                    Email = customer.FirstOrDefault(c => c.Id == newProject.CustomerId).Email,
                    Address = customer.FirstOrDefault(c => c.Id == newProject.CustomerId).Address,
                    Description = customer.FirstOrDefault(c => c.Id == newProject.CustomerId).Description,
                    CustomerCode = customer.FirstOrDefault(c => c.Id == newProject.CustomerId).CustomerCode,
                    TaxCode = customer.FirstOrDefault(c => c.Id == newProject.CustomerId).TaxCode,
                    Fax = customer.FirstOrDefault(c => c.Id == newProject.CustomerId).Fax,
                    BankAccount = customer.FirstOrDefault(c => c.Id == newProject.CustomerId).BankAccount,
                    BankName = customer.FirstOrDefault(c => c.Id == newProject.CustomerId).BankName,
                    CreatedAt = customer.FirstOrDefault(c => c.Id == newProject.CustomerId).CreatedAt,
                    Creator = customer.FirstOrDefault(c => c.Id == newProject.CustomerId).Creator,
                    UpdatedAt = customer.FirstOrDefault(c => c.Id == newProject.CustomerId).UpdatedAt,
                    Updater = customer.FirstOrDefault(c => c.Id == newProject.CustomerId).Updater,
                    Deleted = customer.FirstOrDefault(c => c.Id == newProject.CustomerId).Deleted,
                    Projects = null // Prevent circular references
                } : new Customer(),
                ConstructType = newProject.ConstructType,
                Location = newProject.Location,
                Area = newProject.Area,
                Purpose = newProject.Purpose,
                TechnicalReqs = newProject.TechnicalReqs,
                StartDate = newProject.StartDate,
                EndDate = newProject.EndDate,
                Budget = newProject.Budget,
                Status = newProject.Status,
                Attachments = newProject.Attachments != null ? 
                    System.Text.Json.JsonSerializer.Deserialize<List<AttachmentInfo>>(newProject.Attachments.RootElement.ToString()) 
                    : null,
                Description = newProject.Description,
                UpdatedAt = newProject.UpdatedAt,
                Updater = newProject.Updater,
                CreatedAt = newProject.CreatedAt,
                Creator = newProject.Creator,
                Deleted = newProject.Deleted,
                IsCreator = true, // Người tạo luôn là true khi gọi Save
                ViewerUserIds = await _context.ProjectUsers
                    .Where(pu => pu.ProjectId == newProject.Id && !pu.IsCreator && !pu.Deleted)
                    .Select(pu => pu.UserId)
                    .ToListAsync()
            };
            
            return projectDTO;
        }

        /// <summary>
        /// Updates a project's status based on Executive Board approval
        /// </summary>
        /// <param name="model">The update status model</param>
        /// <param name="actionBy">ID of the user performing the action</param>
        /// <returns>True if the update was successful</returns>
        public async Task<bool> UpdateStatus(UpdateProjectStatusDTO model, int actionBy)
        {
            // Verify user is part of the Executive Board
            if (!_helperService.IsInRole(actionBy, RoleConstValue.EXECUTIVE_BOARD))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            
            // Find the project
            var project = await _context.Projects
                .FirstOrDefaultAsync(p => p.Id == model.ProjectId && !p.Deleted);
                
            if (project == null)
            {
                throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
            }
            
            // Start transaction
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                // Check if the status change is valid
                ValidateStatusChange(project.Status, model.TargetStatus);
                
                // If status is changing to Completed or Closed, roll back materials
                if ((model.TargetStatus == ProjectStatusEnum.Completed || model.TargetStatus == ProjectStatusEnum.Closed) &&
                    project.Status != ProjectStatusEnum.Completed && project.Status != ProjectStatusEnum.Closed)
                {
                    // Roll back materials to main inventory
                    var rollbackCount = await _materialService.RollbackMaterialsFromProjectInventory(project.Id, actionBy);
                    _logger.LogInformation($"Rolled back {rollbackCount} materials from project {project.Id} during status change to {model.TargetStatus}");
                }
                
                // Update project status
                project.Status = model.TargetStatus;
                project.UpdatedAt = DateTime.Now;
                project.Updater = actionBy;
                
                _context.Projects.Update(project);
                await _context.SaveChangesAsync();
                
                // Invalidate project caches
                await InvalidateProjectCaches(project.Id);
                
                await transaction.CommitAsync();
                
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error updating project status: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Validates that the requested status change is allowed
        /// </summary>
        /// <param name="currentStatus">The current project status</param>
        /// <param name="targetStatus">The requested target status</param>
        private void ValidateStatusChange(ProjectStatusEnum currentStatus, ProjectStatusEnum targetStatus)
        {
            switch (targetStatus)
            {
                case ProjectStatusEnum.Planning:
                    // Can only move to Planning from ReceiveRequest
                    if (currentStatus != ProjectStatusEnum.ReceiveRequest)
                    {
                        throw new InvalidOperationException($"Cannot change status to Planning from {currentStatus}. Project must be in ReceiveRequest status.");
                    }
                    break;
                    
                case ProjectStatusEnum.Paused:
                    // Can only pause from InProgress
                    if (currentStatus != ProjectStatusEnum.InProgress)
                    {
                        throw new InvalidOperationException($"Cannot change status to Paused from {currentStatus}. Project must be InProgress.");
                    }
                    break;
                    
                case ProjectStatusEnum.InProgress:
                    // Can only resume from Paused
                    if (currentStatus != ProjectStatusEnum.Paused)
                    {
                        throw new InvalidOperationException($"Cannot change status to InProgress from {currentStatus}. Project must be Paused.");
                    }
                    break;
                    
                case ProjectStatusEnum.Completed:
                    // Can only complete from WaitingApproveCompleted
                    if (currentStatus != ProjectStatusEnum.WaitingApproveCompleted)
                    {
                        throw new InvalidOperationException($"Cannot change status to Completed from {currentStatus}. Project must be WaitingApproveCompleted.");
                    }
                    break;
                    
                case ProjectStatusEnum.Closed:
                    // Can close from any status
                    break;
                    
                default:
                    throw new InvalidOperationException($"Cannot manually change status to {targetStatus}. This status is managed automatically.");
            }
        }
        
        /// <summary>
        /// Invalidates all caches related to a project
        /// </summary>
        /// <param name="projectId">The ID of the project</param>
        private async Task InvalidateProjectCaches(int projectId)
        {
            // Invalidate specific project cache
            string projectCacheKey = string.Format(RedisCacheKey.PROJECT_BY_ID_CACHE_KEY, projectId);
            await _cacheService.DeleteAsync(projectCacheKey);
            
            // Invalidate main project caches
            await _cacheService.DeleteAsync(RedisCacheKey.PROJECT_CACHE_KEY);
            await _cacheService.DeleteAsync(RedisCacheKey.PROJECT_LIST_CACHE_KEY);
            
            // Invalidate project status count cache
            await _cacheService.DeleteAsync(RedisCacheKey.PROJECT_STATUS_CACHE_KEY);
        }
    }
}
