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

            // Delete attachments from Google Drive if they exist
            if (data.Attachments != null)
            {
                try
                {
                    var attachments = System.Text.Json.JsonSerializer.Deserialize<List<AttachmentInfo>>(data.Attachments.RootElement.ToString());
                    if (attachments != null && attachments.Any())
                    {
                        var linksToDelete = attachments.Select(a => a.WebContentLink).ToList();
                        await _googleDriveService.DeleteFilesByLinks(linksToDelete);
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue with deletion
                    Console.WriteLine($"Failed to delete attachments: {ex.Message}");
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
                var entity = data.FirstOrDefault(t => t.Id == model.Id);
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
                Attachments = project.Attachments != null ? 
                    System.Text.Json.JsonSerializer.Deserialize<List<AttachmentInfo>>(project.Attachments.RootElement.ToString()) 
                    : null,
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
