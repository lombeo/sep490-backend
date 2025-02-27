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

            var data = await _context.Projects.FirstOrDefaultAsync(t => t.Id == id);
            if (data == null)
            {
                throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
            }

            // Delete attachment from Google Drive if exists
            if (!string.IsNullOrEmpty(data.Attachment))
            {
                try
                {
                    await _googleDriveService.DeleteFilesByLinks(new List<string> { data.Attachment });
                }
                catch (Exception ex)
                {
                    // Log error but continue with deletion
                    Console.WriteLine($"Failed to delete attachment: {ex.Message}");
                }
            }

            data.Deleted = true;
            _context.Update(data);
            await _context.SaveChangesAsync();

            _ = _cacheService.DeleteAsync(RedisCacheKey.PROJECT_CACHE_KEY);

            return data.Id;
        }

        public async Task<ProjectDTO> Detail(int id, int actionBy)
        {
            if (!_helperService.IsInRole(actionBy, new List<string> { RoleConstValue.BUSINESS_EMPLOYEE, RoleConstValue.EXECUTIVE_BOARD }))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            var data = await _dataService.ListProject(new SearchProjectDTO()
            {
                ActionBy = actionBy,
                PageSize = int.MaxValue
            });
            var result = data.FirstOrDefault(t => t.Id == id);
            if (result == null)
            {
                throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
            }
            return result;
        }

        public async Task<ListProjectStatusDTO> ListProjectStatus(int actionBy)
        {
            if (!_helperService.IsInRole(actionBy, new List<string> { RoleConstValue.BUSINESS_EMPLOYEE, RoleConstValue.EXECUTIVE_BOARD }))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

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

            var customer = await _dataService.ListCustomer(new DTO.Customer.CustomerSearchDTO
            {
                ActionBy = actionBy,
                PageSize = int.MaxValue
            });
            var data = _context.Projects.Where(t => !t.Deleted).ToList();

            if (model.StartDate > model.EndDate)
            {
                throw new ArgumentException(Message.ProjectMessage.INVALID_DATE);
            }
            if (customer.FirstOrDefault(t => t.Id == model.CustomerId) == null)
            {
                throw new KeyNotFoundException(Message.CustomerMessage.CUSTOMER_NOT_FOUND);
            }

            // Handle file attachment
            string attachmentUrl = null; // Reset attachment URL
            if (model.Id != 0)
            {
                // If this is an update, get the existing project to check old attachment
                var existingProject = await _context.Projects.FirstOrDefaultAsync(t => t.Id == model.Id);
                if (existingProject != null)
                {
                    attachmentUrl = existingProject.Attachment; // Keep existing attachment URL by default
                }
            }

            if (model.Attachment != null)
            {
                // If there's an existing attachment and we're uploading a new one, delete the old one
                if (!string.IsNullOrEmpty(attachmentUrl))
                {
                    try
                    {
                        await _googleDriveService.DeleteFilesByLinks(new List<string> { attachmentUrl });
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue with upload
                        Console.WriteLine($"Failed to delete old attachment: {ex.Message}");
                    }
                }

                // Upload new file
                using (var stream = model.Attachment.OpenReadStream())
                {
                    attachmentUrl = await _googleDriveService.UploadFile(
                        stream,
                        model.Attachment.FileName,
                        model.Attachment.ContentType
                    );
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
                Attachment = attachmentUrl,
                Description = model.Description,
                UpdatedAt = DateTime.UtcNow,
                Updater = actionBy
            };

            if (model.Id != 0)
            {
                var entity = data.FirstOrDefault(t => t.Id == model.Id);
                if (entity == null)
                {
                    throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
                }
                if (data.FirstOrDefault(t => t.ProjectCode == model.ProjectCode && t.ProjectCode != entity.ProjectCode) != null)
                {
                    throw new ArgumentException(Message.ProjectMessage.PROJECT_CODE_EXIST);
                }
                project.ProjectCode = model.ProjectCode;
                project.CreatedAt = entity.CreatedAt;
                project.Creator = entity.Creator;
                project.Id = model.Id;

                _context.Update(project);
            }
            else
            {
                if (data.FirstOrDefault(t => t.ProjectCode == model.ProjectCode) != null)
                {
                    throw new ArgumentException(Message.ProjectMessage.PROJECT_CODE_EXIST);
                }

                project.ProjectCode = model.ProjectCode;
                project.CreatedAt = DateTime.UtcNow;
                project.Creator = actionBy;

                await _context.AddAsync(project);
            }

            await _context.SaveChangesAsync();
            _ = _cacheService.DeleteAsync(RedisCacheKey.PROJECT_CACHE_KEY);

            return new ProjectDTO
            {
                Id = project.Id,
                ProjectCode = project.ProjectCode,
                ProjectName = project.ProjectName,
                Customer = customer.FirstOrDefault(c => c.Id == project.CustomerId) ?? new Customer(),
                ConstructType = project.ConstructType,
                Location = project.Location,
                Area = project.Area,
                Purpose = project.Purpose,
                TechnicalReqs = project.TechnicalReqs,
                StartDate = project.StartDate,
                EndDate = project.EndDate,
                Budget = project.Budget,
                Status = project.Status,
                Attachment = project.Attachment,
                Description = project.Description,
                UpdatedAt = project.UpdatedAt,
                Updater = project.Updater,
                CreatedAt = project.CreatedAt,
                Creator = project.Creator,
                Deleted = project.Deleted
            };
        }
    }
}
