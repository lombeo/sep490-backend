using Microsoft.EntityFrameworkCore;
using NPOI.HSSF.Record.Chart;
using NPOI.SS.Formula.Functions;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.Project;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Services.CacheService;
using Sep490_Backend.Services.DataService;
using Sep490_Backend.Services.HelperService;
using Sep490_Backend.Services.GoogleDriveService;
using Microsoft.AspNetCore.Http;
using Sep490_Backend.Infra.Helps;
using Sep490_Backend.Controllers;
using System.Text.Json;
using Sep490_Backend.DTO;
using Sep490_Backend.DTO.Customer;

namespace Sep490_Backend.Services.ProjectService
{
    public interface IProjectService
    {
        Task<ProjectDTO> Save(SaveProjectDTO model, int actionBy);
        Task<int> Delete(int id, int actionBy);
        Task<ListProjectStatusDTO> ListProjectStatus(int actionBy);
        Task<ProjectDTO> Detail(int id, int actionBy);
    }

    public class ProjectService : IProjectService
    {
        private readonly BackendContext _context;
        private readonly ICacheService _cacheService;
        private readonly IHelperService _helperService;
        private readonly IDataService _dataService;
        private readonly IGoogleDriveService _googleDriveService;

        public ProjectService(
            BackendContext context, 
            IDataService dataService, 
            ICacheService cacheService, 
            IHelperService helperService,
            IGoogleDriveService googleDriveService)
        {
            _context = context;
            _cacheService = cacheService;
            _helperService = helperService;
            _dataService = dataService;
            _googleDriveService = googleDriveService;
        } 

        public async Task<int> Delete(int id, int actionBy)
        {
            if (!_helperService.IsInRole(actionBy, RoleConstValue.BUSINESS_EMPLOYEE))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            var entity = await _context.Projects.FirstOrDefaultAsync(t => t.Id == id);
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

            // Lấy tất cả các entity liên quan đến project để xóa mềm
            
            // 1. Xóa mềm toàn bộ SiteSurvey liên quan
            var siteSurveys = await _context.SiteSurveys
                .Where(t => t.ProjectId == id && !t.Deleted)
                .ToListAsync();
                
            foreach (var survey in siteSurveys)
            {
                survey.Deleted = true;
                survey.UpdatedAt = DateTime.UtcNow;
                survey.Updater = actionBy;
                _context.Update(survey);
            }
            
            // 2. Xóa mềm toàn bộ Contract liên quan
            var contracts = await _context.Contracts
                .Where(c => c.ProjectId == id && !c.Deleted)
                .ToListAsync();
                
            var contractIds = contracts.Select(c => c.Id).ToList();
                
            foreach (var contract in contracts)
            {
                contract.Deleted = true;
                contract.UpdatedAt = DateTime.UtcNow;
                contract.Updater = actionBy;
                _context.Update(contract);
            }
            
            // 3. Xóa mềm toàn bộ ContractDetail của các Contract liên quan
            if (contractIds.Any())
            {
                var contractDetails = await _context.ContractDetails
                    .Where(cd => contractIds.Contains(cd.ContractId) && !cd.Deleted)
                    .ToListAsync();
                    
                foreach (var detail in contractDetails)
                {
                    detail.Deleted = true;
                    detail.UpdatedAt = DateTime.UtcNow;
                    detail.Updater = actionBy;
                    _context.Update(detail);
                }
            }
            
            // 4. Xóa mềm toàn bộ ProjectUser liên quan
            var projectUsers = await _context.ProjectUsers
                .Where(pu => pu.ProjectId == id && !pu.Deleted)
                .ToListAsync();
                
            foreach (var pu in projectUsers)
            {
                pu.Deleted = true;
                pu.UpdatedAt = DateTime.UtcNow;
                pu.Updater = actionBy;
                _context.Update(pu);
            }
            
            // 5. Cuối cùng, xóa mềm Project
            entity.Deleted = true;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.Updater = actionBy;
            _context.Update(entity);

            // Lấy tất cả người dùng liên quan đến project để xóa cache sau này
            var relatedUsers = projectUsers.Select(pu => pu.UserId).ToList();

            await _context.SaveChangesAsync();
            
            // Simplify cache deletion - only delete the main cache keys
            _ = _cacheService.DeleteAsync(RedisCacheKey.PROJECT_CACHE_KEY);
            _ = _cacheService.DeleteAsync(RedisCacheKey.PROJECT_USER_CACHE_KEY);
            
            // Delete other main cache keys affected
            if (siteSurveys.Any())
            {
                _ = _cacheService.DeleteAsync(RedisCacheKey.SITE_SURVEY_CACHE_KEY);
            }
            
            if (contracts.Any())
            {
                _ = _cacheService.DeleteAsync(RedisCacheKey.CONTRACT_CACHE_KEY);
                _ = _cacheService.DeleteAsync(RedisCacheKey.CONTRACT_DETAIL_CACHE_KEY);
            }
            
            return entity.Id;
        }

        public async Task<ProjectDTO> Detail(int id, int actionBy)
        {
            // Get all projects from the main cache
            var allProjects = await _cacheService.GetAsync<List<ProjectDTO>>(RedisCacheKey.PROJECT_CACHE_KEY);
            
            if (allProjects != null)
            {
                // Filter from main cache by id and check user access
                var projectFromCache = allProjects.FirstOrDefault(p => p.Id == id);
                if (projectFromCache != null && CanUserAccessProject(projectFromCache, actionBy))
                {
                    return projectFromCache;
                }
            }
            
            // If not in cache or user doesn't have access, query from database
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
            
            // Update cache if needed
            if (allProjects == null)
            {
                // If cache is empty, load all projects that are not deleted
                allProjects = await GetAllProjectDTOs();
                await _cacheService.SetAsync(RedisCacheKey.PROJECT_CACHE_KEY, allProjects, TimeSpan.FromHours(1));
            }
            else if (!allProjects.Any(p => p.Id == projectDTO.Id))
            {
                // If project not in cache, add it and update cache
                allProjects.Add(projectDTO);
                await _cacheService.SetAsync(RedisCacheKey.PROJECT_CACHE_KEY, allProjects, TimeSpan.FromHours(1));
            }
            
            return projectDTO;
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
            if (!_helperService.IsInRole(actionBy, RoleConstValue.BUSINESS_EMPLOYEE))
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

            var project = new Project()
            {
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
                Status = model.Status,
                Attachments = attachmentInfos.Any() ? JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(attachmentInfos)) : null,
                Description = model.Description,
                UpdatedAt = DateTime.UtcNow,
                Updater = actionBy
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
                entity.Status = model.Status;
                entity.Attachments = attachmentInfos.Any() ? JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(attachmentInfos)) : null;
                entity.Description = model.Description;
                entity.UpdatedAt = DateTime.UtcNow;
                entity.Updater = actionBy;

                _context.Update(entity);
                project = entity; // Use the updated entity for the return value
                
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

                project.ProjectCode = model.ProjectCode;
                project.CreatedAt = DateTime.UtcNow;
                project.Creator = actionBy;

                await _context.AddAsync(project);
                await _context.SaveChangesAsync(); // Lưu project trước để có Id
                
                // Thêm mới bản ghi người tạo
                var creatorRecord = new ProjectUser
                {
                    ProjectId = project.Id,
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
                    ProjectId = project.Id,
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
            
            // Only delete the main cache key to force reload
            _ = _cacheService.DeleteAsync(RedisCacheKey.PROJECT_CACHE_KEY);
            
            return new ProjectDTO
            {
                Id = project.Id,
                ProjectCode = project.ProjectCode,
                ProjectName = project.ProjectName,
                // Tạo Customer mới với Projects = null để tránh vòng lặp tham chiếu
                Customer = customer.FirstOrDefault(c => c.Id == project.CustomerId) != null ? new Customer
                {
                    Id = customer.FirstOrDefault(c => c.Id == project.CustomerId).Id,
                    CustomerName = customer.FirstOrDefault(c => c.Id == project.CustomerId).CustomerName,
                    DirectorName = customer.FirstOrDefault(c => c.Id == project.CustomerId).DirectorName,
                    Phone = customer.FirstOrDefault(c => c.Id == project.CustomerId).Phone,
                    Email = customer.FirstOrDefault(c => c.Id == project.CustomerId).Email,
                    Address = customer.FirstOrDefault(c => c.Id == project.CustomerId).Address,
                    Description = customer.FirstOrDefault(c => c.Id == project.CustomerId).Description,
                    CustomerCode = customer.FirstOrDefault(c => c.Id == project.CustomerId).CustomerCode,
                    TaxCode = customer.FirstOrDefault(c => c.Id == project.CustomerId).TaxCode,
                    Fax = customer.FirstOrDefault(c => c.Id == project.CustomerId).Fax,
                    BankAccount = customer.FirstOrDefault(c => c.Id == project.CustomerId).BankAccount,
                    BankName = customer.FirstOrDefault(c => c.Id == project.CustomerId).BankName,
                    CreatedAt = customer.FirstOrDefault(c => c.Id == project.CustomerId).CreatedAt,
                    Creator = customer.FirstOrDefault(c => c.Id == project.CustomerId).Creator,
                    UpdatedAt = customer.FirstOrDefault(c => c.Id == project.CustomerId).UpdatedAt,
                    Updater = customer.FirstOrDefault(c => c.Id == project.CustomerId).Updater,
                    Deleted = customer.FirstOrDefault(c => c.Id == project.CustomerId).Deleted,
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
                    System.Text.Json.JsonSerializer.Deserialize<List<AttachmentInfo>>(project.Attachments.RootElement.ToString()) 
                    : null,
                Description = project.Description,
                UpdatedAt = project.UpdatedAt,
                Updater = project.Updater,
                CreatedAt = project.CreatedAt,
                Creator = project.Creator,
                Deleted = project.Deleted,
                IsCreator = true, // Người tạo luôn là true khi gọi Save
                ViewerUserIds = await _context.ProjectUsers
                    .Where(pu => pu.ProjectId == project.Id && !pu.IsCreator && !pu.Deleted)
                    .Select(pu => pu.UserId)
                    .ToListAsync()
            };
        }
    }
}
