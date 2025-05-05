using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sep490_Backend.DTO;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.ConstructionLog;
using Sep490_Backend.DTO.ConstructionProgress;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Infra.Enums;
using Sep490_Backend.Infra.Helps;
using Sep490_Backend.Services.CacheService;
using Sep490_Backend.Services.ConstructionProgressService;
using Sep490_Backend.Services.GoogleDriveService;
using Sep490_Backend.Services.HelperService;
using Sep490_Backend.Services.ProjectService;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Sep490_Backend.Services.ConstructionLogService;

namespace Sep490_Backend.Services.ConstructionLogService
{
    public interface IConstructionLogService
    {
        Task<ConstructionLogDTO> Save(SaveConstructionLogDTO model, int actionBy);
        Task<int> Delete(int id, int actionBy);
        Task<List<ConstructionLogDTO>> List(SearchConstructionLogDTO model);
        Task<ConstructionLogDTO> Detail(int id, int actionBy);
        Task<List<ConstructionLogDTO>> GetByProject(int projectId, int actionBy);
        Task<ResourceLogByTaskDTO> GetResourceLogByTask(int projectId, string taskIndex, int actionBy);
    }

    public class ConstructionLogService : IConstructionLogService
    {
        private readonly BackendContext _context;
        private readonly ICacheService _cacheService;
        private readonly IHelperService _helperService;
        private readonly IGoogleDriveService _googleDriveService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<ConstructionLogService> _logger;
        private readonly IServiceProvider _serviceProvider;

        // Static JsonSerializerOptions to be reused across all methods
        private static readonly JsonSerializerOptions DefaultSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new DateTimeJsonConverter() }
        };

        public ConstructionLogService(
            BackendContext context,
            ICacheService cacheService,
            IHelperService helperService,
            IGoogleDriveService googleDriveService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<ConstructionLogService> logger,
            IServiceProvider serviceProvider)
        {
            _context = context;
            _cacheService = cacheService;
            _helperService = helperService;
            _googleDriveService = googleDriveService;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task<ConstructionLogDTO> Save(SaveConstructionLogDTO model, int actionBy)
        {
            // If only updating the status (approve/reject), handle specially
            if (model.Id != 0 && model.Status.HasValue && 
                (model.LogName == null && model.ProjectId == 0 && model.LogDate == default))
            {
                return await UpdateConstructionLogStatus(model.Id, model.Status.Value, actionBy);
            }

            // Check if user is a Construction Manager
            if (!_helperService.IsInRole(actionBy, RoleConstValue.CONSTRUCTION_MANAGER) &&
                !_helperService.IsInRole(actionBy, RoleConstValue.EXECUTIVE_BOARD))
            {
                throw new UnauthorizedAccessException(Message.ConstructionLogMessage.ONLY_CONSTRUCTION_MANAGER);
            }

            // Check if project exists
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == model.ProjectId && !p.Deleted);
            if (project == null)
            {
                throw new KeyNotFoundException(Message.ConstructionLogMessage.PROJECT_NOT_FOUND);
            }

            // Check if user has access to the project
            var hasAccess = await _context.ProjectUsers
                .AnyAsync(pu => pu.ProjectId == model.ProjectId && pu.UserId == actionBy && !pu.Deleted);
                
            if (!hasAccess && !_helperService.IsInRole(actionBy, RoleConstValue.EXECUTIVE_BOARD))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Generate LogCode based on Project Code
            string logCode;
            if (model.Id == 0) // Creating new log
            {
                // Get the latest index for this project
                int nextIndex = 1;
                var existingLogs = await _context.ConstructionLogs
                    .Where(cl => cl.ProjectId == model.ProjectId && !cl.Deleted)
                    .ToListAsync();
                
                if (existingLogs.Any())
                {
                    // Extract indices from existing log codes that match the pattern
                    var pattern = $"{project.ProjectCode}_Log_";
                    var indices = existingLogs
                        .Where(cl => cl.LogCode.StartsWith(pattern))
                        .Select(cl => 
                        {
                            if (int.TryParse(cl.LogCode.Substring(pattern.Length), out int index))
                                return index;
                            return 0;
                        })
                        .Where(i => i > 0)
                        .ToList();
                    
                    if (indices.Any())
                    {
                        nextIndex = indices.Max() + 1;
                    }
                }
                
                logCode = $"{project.ProjectCode}_Log_{nextIndex}";
            }
            else // Updating existing log
            {
                var existingLog = await _context.ConstructionLogs.FirstOrDefaultAsync(cl => cl.Id == model.Id && !cl.Deleted);
                
                if (existingLog == null)
                {
                    throw new KeyNotFoundException(Message.ConstructionLogMessage.NOT_FOUND);
                }
                
                // Keep existing log code
                logCode = existingLog.LogCode;
            }

            // Handle file attachments
            List<AttachmentInfo> attachmentInfos = new List<AttachmentInfo>();
            List<AttachmentInfo> imageInfos = new List<AttachmentInfo>();
            
            // Get existing attachments if updating
            if (model.Id != 0)
            {
                var existingLog = await _context.ConstructionLogs.FirstOrDefaultAsync(cl => cl.Id == model.Id && !cl.Deleted);
                if (existingLog?.Attachments != null)
                {
                    attachmentInfos = JsonSerializer.Deserialize<List<AttachmentInfo>>(existingLog.Attachments.RootElement.ToString());
                    
                    // Delete old attachments if there are new ones
                    if (model.AttachmentFiles != null && model.AttachmentFiles.Any())
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
                }
                
                // Get existing images if updating
                if (existingLog?.Images != null)
                {
                    imageInfos = JsonSerializer.Deserialize<List<AttachmentInfo>>(existingLog.Images.RootElement.ToString());
                    
                    // Delete old images if there are new ones - check for field name "images" which is a file in the form
                    if (model.ImageFiles != null && model.ImageFiles.Any() || model.ImageFile != null || 
                        (_httpContextAccessor.HttpContext?.Request.Form.Files != null && 
                         _httpContextAccessor.HttpContext.Request.Form.Files.Any(f => f.Name == "images")))
                    {
                        try
                        {
                            var linksToDelete = imageInfos.Select(a => a.WebContentLink).ToList();
                            await _googleDriveService.DeleteFilesByLinks(linksToDelete);
                            imageInfos.Clear();
                        }
                        catch (Exception ex)
                        {
                            // Log error but continue with upload
                            Console.WriteLine($"Failed to delete old images: {ex.Message}");
                        }
                    }
                }
            }

            // Upload new attachments
            if (model.AttachmentFiles != null && model.AttachmentFiles.Any())
            {
                foreach (var file in model.AttachmentFiles)
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
            
            // Handle single image file from form field named "images"
            var imagesFiles = _httpContextAccessor.HttpContext?.Request.Form.Files
                .Where(f => f.Name == "images").ToList();
            
            if (imagesFiles != null && imagesFiles.Any())
            {
                foreach (var file in imagesFiles)
                {
                    // Validate image file type
                    if (!_googleDriveService.IsValidImageFile(file.FileName, file.ContentType))
                    {
                        throw new ArgumentException($"Invalid image file type: {file.FileName}. Only JPEG, PNG, GIF, BMP, WebP and TIFF are allowed.");
                    }
                    
                    using (var stream = file.OpenReadStream())
                    {
                        var uploadResult = await _googleDriveService.UploadFile(
                            stream,
                            file.FileName,
                            file.ContentType
                        );

                        // Parse Google Drive response to get file ID
                        var fileId = uploadResult.Split("id=").Last().Split("&").First();
                        
                        imageInfos.Add(new AttachmentInfo
                        {
                            Id = fileId,
                            Name = file.FileName,
                            WebViewLink = $"https://drive.google.com/file/d/{fileId}/view",
                            WebContentLink = uploadResult
                        });
                    }
                }
            }
            // If no images found directly in the form, try the ImageFiles or ImageFile properties
            else if (model.ImageFiles != null && model.ImageFiles.Any())
            {
                // Upload new images
                foreach (var file in model.ImageFiles)
                {
                    // Validate image file type
                    if (!_googleDriveService.IsValidImageFile(file.FileName, file.ContentType))
                    {
                        throw new ArgumentException($"Invalid image file type: {file.FileName}. Only JPEG, PNG, GIF, BMP, WebP and TIFF are allowed.");
                    }
                    
                    using (var stream = file.OpenReadStream())
                    {
                        var uploadResult = await _googleDriveService.UploadFile(
                            stream,
                            file.FileName,
                            file.ContentType
                        );

                        // Parse Google Drive response to get file ID
                        var fileId = uploadResult.Split("id=").Last().Split("&").First();
                        
                        imageInfos.Add(new AttachmentInfo
                        {
                            Id = fileId,
                            Name = file.FileName,
                            WebViewLink = $"https://drive.google.com/file/d/{fileId}/view",
                            WebContentLink = uploadResult
                        });
                    }
                }
            }
            // Handle single image file upload (for backward compatibility with frontend)
            else if (model.ImageFile != null)
            {
                var file = model.ImageFile;
                
                // Validate image file type
                if (!_googleDriveService.IsValidImageFile(file.FileName, file.ContentType))
                {
                    throw new ArgumentException($"Invalid image file type: {file.FileName}. Only JPEG, PNG, GIF, BMP, WebP and TIFF are allowed.");
                }
                
                using (var stream = file.OpenReadStream())
                {
                    var uploadResult = await _googleDriveService.UploadFile(
                        stream,
                        file.FileName,
                        file.ContentType
                    );

                    // Parse Google Drive response to get file ID
                    var fileId = uploadResult.Split("id=").Last().Split("&").First();
                    
                    imageInfos.Add(new AttachmentInfo
                    {
                        Id = fileId,
                        Name = file.FileName,
                        WebViewLink = $"https://drive.google.com/file/d/{fileId}/view",
                        WebContentLink = uploadResult
                    });
                }
            }

            ConstructionLog constructionLog;
            
            if (model.Id != 0) // Update existing log
            {
                constructionLog = await _context.ConstructionLogs.FirstOrDefaultAsync(cl => cl.Id == model.Id && !cl.Deleted);
                
                if (constructionLog == null)
                {
                    throw new KeyNotFoundException(Message.ConstructionLogMessage.NOT_FOUND);
                }
                
                // Update properties
                constructionLog.ProjectId = model.ProjectId;
                constructionLog.LogCode = logCode; // Use the auto-generated or existing log code
                constructionLog.LogName = model.LogName;
                constructionLog.LogDate = model.LogDate;
                constructionLog.Resources = model.Resources != null 
                    ? JsonDocument.Parse(JsonSerializer.Serialize(model.Resources, DefaultSerializerOptions))
                    : JsonDocument.Parse(JsonSerializer.Serialize(new List<ConstructionLogResourceDTO>(), DefaultSerializerOptions));
                constructionLog.WorkAmount = model.WorkAmount != null 
                    ? JsonDocument.Parse(JsonSerializer.Serialize(model.WorkAmount, DefaultSerializerOptions))
                    : JsonDocument.Parse(JsonSerializer.Serialize(new List<WorkAmountDTO>(), DefaultSerializerOptions));
                constructionLog.Weather = model.Weather != null 
                    ? JsonDocument.Parse(JsonSerializer.Serialize(model.Weather, DefaultSerializerOptions))
                    : JsonDocument.Parse(JsonSerializer.Serialize(new List<WeatherDTO>(), DefaultSerializerOptions));
                constructionLog.Safety = model.Safety;
                constructionLog.Quality = model.Quality;
                constructionLog.Progress = model.Progress;
                constructionLog.Problem = model.Problem;
                constructionLog.Advice = model.Advice;
                constructionLog.Images = imageInfos.Any() 
                    ? JsonDocument.Parse(JsonSerializer.Serialize(imageInfos, DefaultSerializerOptions))
                    : (model.Images != null && model.Images.Any()
                        ? JsonDocument.Parse(JsonSerializer.Serialize(model.Images.Select(url => new AttachmentInfo
                        {
                            WebContentLink = url,
                            WebViewLink = url,
                            Name = "Legacy Image",
                            Id = Guid.NewGuid().ToString()
                        }).ToList(), DefaultSerializerOptions))
                        : JsonDocument.Parse(JsonSerializer.Serialize(new List<AttachmentInfo>(), DefaultSerializerOptions)));
                constructionLog.Attachments = attachmentInfos.Any() 
                    ? JsonDocument.Parse(JsonSerializer.Serialize(attachmentInfos, DefaultSerializerOptions))
                    : JsonDocument.Parse(JsonSerializer.Serialize(new List<AttachmentInfo>(), DefaultSerializerOptions));
                constructionLog.Note = model.Note;
                
                // Update Status if provided, otherwise reset to WaitingForApproval if there are significant changes
                if (model.Status.HasValue)
                {
                    constructionLog.Status = model.Status.Value;
                }
                else if (HasSignificantChanges(constructionLog, model))
                {
                    constructionLog.Status = ConstructionLogStatus.WaitingForApproval;
                }
                
                constructionLog.UpdatedAt = DateTime.UtcNow;
                constructionLog.Updater = actionBy;
                
                _context.Update(constructionLog);
            }
            else // Create new log
            {
                constructionLog = new ConstructionLog
                {
                    ProjectId = model.ProjectId,
                    LogCode = logCode, // Use the auto-generated log code
                    LogName = model.LogName,
                    LogDate = model.LogDate,
                    Resources = model.Resources != null 
                        ? JsonDocument.Parse(JsonSerializer.Serialize(model.Resources, DefaultSerializerOptions))
                        : JsonDocument.Parse(JsonSerializer.Serialize(new List<ConstructionLogResourceDTO>(), DefaultSerializerOptions)),
                    WorkAmount = model.WorkAmount != null 
                        ? JsonDocument.Parse(JsonSerializer.Serialize(model.WorkAmount, DefaultSerializerOptions))
                        : JsonDocument.Parse(JsonSerializer.Serialize(new List<WorkAmountDTO>(), DefaultSerializerOptions)),
                    Weather = model.Weather != null 
                        ? JsonDocument.Parse(JsonSerializer.Serialize(model.Weather, DefaultSerializerOptions))
                        : JsonDocument.Parse(JsonSerializer.Serialize(new List<WeatherDTO>(), DefaultSerializerOptions)),
                    Safety = model.Safety,
                    Quality = model.Quality,
                    Progress = model.Progress,
                    Problem = model.Problem,
                    Advice = model.Advice,
                    Images = imageInfos.Any() 
                        ? JsonDocument.Parse(JsonSerializer.Serialize(imageInfos, DefaultSerializerOptions))
                        : (model.Images != null && model.Images.Any()
                            ? JsonDocument.Parse(JsonSerializer.Serialize(model.Images.Select(url => new AttachmentInfo
                            {
                                WebContentLink = url,
                                WebViewLink = url,
                                Name = "Legacy Image",
                                Id = Guid.NewGuid().ToString()
                            }).ToList(), DefaultSerializerOptions))
                            : JsonDocument.Parse(JsonSerializer.Serialize(new List<AttachmentInfo>(), DefaultSerializerOptions))),
                    Attachments = attachmentInfos.Any() 
                        ? JsonDocument.Parse(JsonSerializer.Serialize(attachmentInfos, DefaultSerializerOptions))
                        : JsonDocument.Parse(JsonSerializer.Serialize(new List<AttachmentInfo>(), DefaultSerializerOptions)),
                    Note = model.Note,
                    Status = model.Status ?? ConstructionLogStatus.WaitingForApproval,
                    CreatedAt = DateTime.UtcNow,
                    Creator = actionBy,
                    UpdatedAt = DateTime.UtcNow,
                    Updater = actionBy,
                    Deleted = false
                };
                
                await _context.AddAsync(constructionLog);
            }
            
            await _context.SaveChangesAsync();
            
            // Invalidate related caches
            await InvalidateConstructionLogCaches(constructionLog.Id, constructionLog.ProjectId);
            
            // Map to DTO for return
            var result = await MapToConstructionLogDTO(constructionLog);
            
            return result;
        }

        private async Task<ConstructionLogDTO> UpdateConstructionLogStatus(int id, ConstructionLogStatus status, int actionBy)
        {
            // Allow both Technical Manager and Executive Board to approve/reject
            if (!_helperService.IsInRole(actionBy, RoleConstValue.TECHNICAL_MANAGER) &&
                !_helperService.IsInRole(actionBy, RoleConstValue.EXECUTIVE_BOARD))
            {
                throw new UnauthorizedAccessException(Message.ConstructionLogMessage.ONLY_TECHNICAL_MANAGER);
            }
            
            var constructionLog = await _context.ConstructionLogs.FirstOrDefaultAsync(cl => cl.Id == id && !cl.Deleted);
            
            if (constructionLog == null)
            {
                throw new KeyNotFoundException(Message.ConstructionLogMessage.NOT_FOUND);
            }
            
            // Check if the log is in the waiting for approval state
            if (constructionLog.Status != ConstructionLogStatus.WaitingForApproval)
            {
                if (status == ConstructionLogStatus.Approved)
                {
                    throw new InvalidOperationException(Message.ConstructionLogMessage.ONLY_WAITING_APPROVAL_CAN_BE_APPROVED);
                }
                else if (status == ConstructionLogStatus.Rejected)
                {
                    throw new InvalidOperationException(Message.ConstructionLogMessage.ONLY_WAITING_APPROVAL_CAN_BE_REJECTED);
                }
            }
            
            constructionLog.Status = status;
            constructionLog.UpdatedAt = DateTime.UtcNow;
            constructionLog.Updater = actionBy;
            
            _context.Update(constructionLog);
            await _context.SaveChangesAsync();
            
            // If the construction log is approved, update the ConstructionProgress records
            if (status == ConstructionLogStatus.Approved)
            {
                await UpdateConstructionProgressFromLog(constructionLog);
                
                // Note: DB changes have been made here, now we can safely invalidate caches 
                // after all data modifications are committed
                
                // Perform comprehensive cache invalidation for all affected entities
                await InvalidateAllRelatedCaches(constructionLog.Id, constructionLog.ProjectId);
            }
            else
            {
                // For non-approval status changes, just invalidate construction log caches
                await InvalidateConstructionLogCaches(constructionLog.Id, constructionLog.ProjectId);
            }
            
            // Get the fully updated construction log DTO to return
            var result = await MapToConstructionLogDTO(constructionLog);
            
            // Trigger email notification in the background (non-blocking)
            // This needs to be injected via the service provider to avoid circular dependencies
            using (var scope = _httpContextAccessor.HttpContext?.RequestServices.CreateScope())
            {
                var emailService = scope?.ServiceProvider.GetService<IConstructionLogEmailService>();
                if (emailService != null)
                {
                    // Fire and forget - don't await this
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await emailService.SendConstructionLogStatusNotification(constructionLog.Id, status, actionBy);
                        }
                        catch (Exception ex)
                        {
                            // Log the error but don't stop execution
                            Console.WriteLine($"Error sending email notification: {ex.Message}");
                        }
                    });
                }
            }
            
            return result;
        }

        /// <summary>
        /// Updates the ConstructionProgress records based on an approved ConstructionLog
        /// </summary>
        /// <param name="constructionLog">The approved construction log</param>
        private async Task UpdateConstructionProgressFromLog(ConstructionLog constructionLog)
        {
            try
            {
                // Get the construction progress for the project
                var constructionProgress = await _context.ConstructionProgresses
                    .Include(cp => cp.ProgressItems)
                        .ThenInclude(pi => pi.Details)
                    .FirstOrDefaultAsync(cp => cp.ProjectId == constructionLog.ProjectId && !cp.Deleted);

                if (constructionProgress == null)
                {
                    // No progress record found for this project
                    return;
                }

                // Track items that have been updated to recalculate parent progress
                var updatedItemIndices = new HashSet<string>();

                // Process WorkAmount entries
                if (constructionLog.WorkAmount != null)
                {
                    var workAmounts = JsonSerializer.Deserialize<List<WorkAmountDTO>>(
                        constructionLog.WorkAmount.RootElement.ToString(),
                        DefaultSerializerOptions);

                    if (workAmounts != null && workAmounts.Any())
                    {
                        foreach (var workAmount in workAmounts)
                        {
                            // Find the matching progress item by task index
                            var progressItem = constructionProgress.ProgressItems
                                .FirstOrDefault(pi => pi.Index == workAmount.TaskIndex && !pi.Deleted);

                            if (progressItem != null)
                            {
                                // Check if this is the first approved log for this task
                                bool isFirstApprovedLog = false;
                                if (progressItem.ActualStartDate == null)
                                {
                                    // Check if there were any previously approved logs for this task
                                    var previousApprovedLogs = await _context.ConstructionLogs
                                        .Where(cl => cl.ProjectId == constructionLog.ProjectId 
                                               && cl.Id != constructionLog.Id 
                                               && cl.Status == ConstructionLogStatus.Approved 
                                               && !cl.Deleted)
                                        .ToListAsync();

                                    isFirstApprovedLog = true;
                                    
                                    foreach (var prevLog in previousApprovedLogs)
                                    {
                                        if (prevLog.WorkAmount != null)
                                        {
                                            var prevWorkAmounts = JsonSerializer.Deserialize<List<WorkAmountDTO>>(
                                                prevLog.WorkAmount.RootElement.ToString(),
                                                DefaultSerializerOptions);
                                                
                                            if (prevWorkAmounts != null && 
                                                prevWorkAmounts.Any(wa => wa.TaskIndex == workAmount.TaskIndex && wa.WorkAmount > 0))
                                            {
                                                isFirstApprovedLog = false;
                                                break;
                                            }
                                        }
                                    }
                                    
                                    // If this is the first approved log for this task, set the actual start date
                                    if (isFirstApprovedLog)
                                    {
                                        progressItem.ActualStartDate = constructionLog.UpdatedAt ?? DateTime.UtcNow;
                                        progressItem.Status = ProgressStatusEnum.InProgress;
                                    }
                                }

                                // Update the UsedQuantity for the progress item
                                progressItem.UsedQuantity += workAmount.WorkAmount;
                                
                                // Calculate and update progress percentage
                                if (progressItem.Quantity > 0)
                                {
                                    progressItem.Progress = (int)Math.Min(100, Math.Round((double)progressItem.UsedQuantity / (double)progressItem.Quantity * 100));
                                    
                                    if (progressItem.Progress >= 100)
                                    {
                                        progressItem.Status = ProgressStatusEnum.WaitForInspection;
                                    }
                                }
                                
                                _context.ConstructionProgressItems.Update(progressItem);
                                
                                // Add this item index to the set of updated items
                                updatedItemIndices.Add(progressItem.Index);
                                
                                // If this item has a parent, add parent index to be updated as well
                                if (!string.IsNullOrEmpty(progressItem.ParentIndex))
                                {
                                    updatedItemIndices.Add(progressItem.ParentIndex);
                                }
                            }
                        }
                    }
                }

                // Process Resources entries
                if (constructionLog.Resources != null)
                {
                    var resources = JsonSerializer.Deserialize<List<ConstructionLogResourceDTO>>(
                        constructionLog.Resources.RootElement.ToString(),
                        DefaultSerializerOptions);

                    if (resources != null && resources.Any())
                    {
                        foreach (var resource in resources)
                        {
                            // Find the matching progress item by task index
                            var progressItem = constructionProgress.ProgressItems
                                .FirstOrDefault(pi => pi.Index == resource.TaskIndex && !pi.Deleted);

                            if (progressItem != null)
                            {
                                // Find the matching progress item detail by resource ID and type
                                var progressItemDetail = progressItem.Details
                                    .FirstOrDefault(d => d.ResourceId == resource.ResourceId && 
                                                        d.ResourceType == (ResourceType)resource.ResourceType &&
                                                        !d.Deleted);

                                if (progressItemDetail != null)
                                {
                                    // Update the UsedQuantity for the progress item detail
                                    progressItemDetail.UsedQuantity += (int)resource.Quantity;
                                    _context.ConstructionProgressItemDetails.Update(progressItemDetail);
                                }
                            }
                        }
                    }
                }

                // If any progress items were updated and they have parent items, update parent progress
                if (updatedItemIndices.Count > 0)
                {
                    // Get all progress items for this progress
                    var allProgressItems = await _context.ConstructionProgressItems
                        .Where(pi => pi.ProgressId == constructionProgress.Id && !pi.Deleted)
                        .ToListAsync();
                    
                    // Update progress of parent items based on their children
                    await UpdateParentItemsProgress(allProgressItems, updatedItemIndices, constructionLog.Updater);
                }

                await _context.SaveChangesAsync();
                
                // Check if project status needs to be updated
                await UpdateProjectStatusBasedOnProgress(constructionLog.ProjectId, constructionLog.Updater);
                
                await _context.SaveChangesAsync();
                
                // Invalidate construction progress caches after updates
                if (constructionProgress != null)
                {
                    await InvalidateConstructionProgressCaches(constructionProgress.Id, constructionProgress.ProjectId, constructionProgress.PlanId);
                    
                    // Also invalidate project caches since the project status might have changed
                    await InvalidateProjectCaches(constructionLog.ProjectId);
                }
            }
            catch (Exception ex)
            {
                // Log the error but don't throw it to avoid disrupting the approval process
                Console.WriteLine($"Error updating construction progress: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the progress of parent items based on the progress of their children
        /// </summary>
        /// <param name="allItems">All progress items for the construction progress</param>
        /// <param name="updatedIndices">Set of indices that were updated and need parent progress recalculation</param>
        /// <param name="actionBy">ID of the user performing the action</param>
        /// <returns>Async task</returns>
        private async Task UpdateParentItemsProgress(List<ConstructionProgressItem> allItems, HashSet<string> updatedIndices, int actionBy)
        {
            Console.WriteLine($"Updating parent items progress for {updatedIndices.Count} potentially affected indices");
            
            // Create a dictionary of items by index for easy lookup
            var itemsByIndex = allItems.ToDictionary(item => item.Index, item => item);
            
            // Create a dictionary to group items by their parent index
            var childrenByParentIndex = allItems
                .Where(item => !string.IsNullOrEmpty(item.ParentIndex))
                .GroupBy(item => item.ParentIndex)
                .ToDictionary(group => group.Key, group => group.ToList());
            
            // Keep track of processed parent indices to avoid processing the same parent multiple times
            var processedParentIndices = new HashSet<string>();
            
            // Process updated items that are either parents or have a parent
            foreach (var index in updatedIndices)
            {
                // Check if this index has children (is a parent)
                if (childrenByParentIndex.ContainsKey(index) && !processedParentIndices.Contains(index))
                {
                    // Get the parent item
                    if (itemsByIndex.TryGetValue(index, out var parentItem))
                    {
                        // Calculate average progress of all child items
                        var childItems = childrenByParentIndex[index];
                        
                        // Skip calculating parent progress if this is a manually set item or not a parent of other items
                        if (childItems.Count > 0)
                        {
                            int totalProgress = 0;
                            foreach (var child in childItems)
                            {
                                totalProgress += child.Progress;
                            }
                            
                            int averageProgress = totalProgress / childItems.Count;
                            
                            Console.WriteLine($"Setting progress of parent item {parentItem.Id} (index {index}) to {averageProgress}% based on {childItems.Count} children");
                            
                            // Update parent progress
                            parentItem.Progress = averageProgress;
                            
                            // Check specific status conditions for the parent based on children statuses
                            
                            // 1. If ALL children have status Done, parent should be Done
                            bool allChildrenDone = childItems.All(c => c.Status == ProgressStatusEnum.Done);
                            
                            // 2. If ANY child has status InProgress, parent should be InProgress
                            bool anyChildInProgress = childItems.Any(c => c.Status == ProgressStatusEnum.InProgress);
                            
                            // 3. If ALL children have status WaitForInspection or Done, parent should be WaitForInspection
                            bool allChildrenWaitingOrDone = childItems.All(c => 
                                c.Status == ProgressStatusEnum.WaitForInspection || 
                                c.Status == ProgressStatusEnum.Done);
                            
                            // 4. If ALL children have status NotStarted, parent should be NotStarted
                            bool allChildrenNotStarted = childItems.All(c => c.Status == ProgressStatusEnum.NotStarted);
                            
                            // Update status based on the conditions above (priority order matters)
                            if (allChildrenDone)
                            {
                                // If all children are Done, set parent to Done
                                parentItem.Status = ProgressStatusEnum.Done;
                                Console.WriteLine($"Setting parent item {parentItem.Id} status to Done because all children are Done");
                            }
                            else if (anyChildInProgress)
                            {
                                // If any child is InProgress, set parent to InProgress
                                parentItem.Status = ProgressStatusEnum.InProgress;
                                Console.WriteLine($"Setting parent item {parentItem.Id} status to InProgress because at least one child is InProgress");
                            }
                            else if (allChildrenWaitingOrDone)
                            {
                                // If all children are either WaitForInspection or Done, set parent to WaitForInspection
                                parentItem.Status = ProgressStatusEnum.WaitForInspection;
                                Console.WriteLine($"Setting parent item {parentItem.Id} status to WaitForInspection because all children are WaitForInspection or Done");
                            }
                            else if (allChildrenNotStarted)
                            {
                                // If all children are NotStarted, set parent to NotStarted
                                parentItem.Status = ProgressStatusEnum.NotStarted;
                                Console.WriteLine($"Setting parent item {parentItem.Id} status to NotStarted because all children are NotStarted");
                            }
                            else 
                            {
                                // Default fallback based on progress value
                                if (averageProgress > 0 && averageProgress < 100)
                                {
                                    parentItem.Status = ProgressStatusEnum.InProgress;
                                    Console.WriteLine($"Setting parent item {parentItem.Id} status to InProgress based on progress value");
                                }
                                else if (averageProgress == 100)
                                {
                                    parentItem.Status = ProgressStatusEnum.WaitForInspection;
                                    Console.WriteLine($"Setting parent item {parentItem.Id} status to WaitForInspection based on progress value");
                                }
                                else
                                {
                                    parentItem.Status = ProgressStatusEnum.NotStarted;
                                    Console.WriteLine($"Setting parent item {parentItem.Id} status to NotStarted based on progress value");
                                }
                            }
                            
                            parentItem.UpdatedAt = DateTime.Now;
                            parentItem.Updater = actionBy;
                            
                            _context.ConstructionProgressItems.Update(parentItem);
                            
                            // Mark this parent as processed
                            processedParentIndices.Add(index);
                            
                            // If this parent has a parent, add its index to be processed
                            if (!string.IsNullOrEmpty(parentItem.ParentIndex) && !processedParentIndices.Contains(parentItem.ParentIndex))
                            {
                                updatedIndices.Add(parentItem.ParentIndex);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Checks and updates project status based on the completion of all progress items
        /// </summary>
        private async Task UpdateProjectStatusBasedOnProgress(int projectId, int actionBy)
        {
            // Get the project
            var project = await _context.Projects
                .FirstOrDefaultAsync(p => p.Id == projectId && !p.Deleted);
            
            if (project == null)
            {
                Console.WriteLine($"Project with ID {projectId} not found or deleted");
                return;
            }
            
            // Get all progress items for this project across all construction progresses
            var allProgressItems = await _context.ConstructionProgresses
                .Where(cp => cp.ProjectId == projectId && !cp.Deleted)
                .SelectMany(cp => cp.ProgressItems.Where(pi => !pi.Deleted))
                .ToListAsync();
            
            if (!allProgressItems.Any())
            {
                Console.WriteLine($"No progress items found for project {projectId}");
                return;
            }
            
            // Determine if all progress items are completed (status = Done)
            bool allCompleted = allProgressItems.All(pi => pi.Status == ProgressStatusEnum.Done);
            bool anyIncomplete = allProgressItems.Any(pi => pi.Status != ProgressStatusEnum.Done);
            
            // Track if we're changing the status
            bool statusChanged = false;
            ProjectStatusEnum newStatus = project.Status;
            
            // If the project is in WaitingApproveCompleted status but we have incomplete items,
            // change back to InProgress
            if (project.Status == ProjectStatusEnum.WaitingApproveCompleted && anyIncomplete)
            {
                Console.WriteLine($"Project {projectId} has incomplete items, changing status from WaitingApproveCompleted to InProgress");
                newStatus = ProjectStatusEnum.InProgress;
                project.Status = ProjectStatusEnum.InProgress;
                project.UpdatedAt = DateTime.Now;
                project.Updater = actionBy;
                _context.Projects.Update(project);
                statusChanged = true;
            }
            // If all items are Done and project is not already waiting for completion approval,
            // change to WaitingApproveCompleted
            else if (allCompleted && project.Status != ProjectStatusEnum.WaitingApproveCompleted && 
                    project.Status != ProjectStatusEnum.Completed && project.Status != ProjectStatusEnum.Closed)
            {
                Console.WriteLine($"All progress items for project {projectId} are marked as Done, changing status to WaitingApproveCompleted");
                newStatus = ProjectStatusEnum.WaitingApproveCompleted;
                project.Status = ProjectStatusEnum.WaitingApproveCompleted;
                project.UpdatedAt = DateTime.Now;
                project.Updater = actionBy;
                _context.Projects.Update(project);
                statusChanged = true;
            }
            
            // Send email notification if status changed
            if (statusChanged)
            {
                try
                {
                    var emailService = _serviceProvider.GetService<IProjectEmailService>();
                    if (emailService != null)
                    {
                        // Queue a background task to send emails
                        _ = Task.Run(async () =>
                        {
                            await emailService.SendProjectStatusChangeNotification(projectId, newStatus, actionBy);
                        });
                    }
                }
                catch (Exception ex)
                {
                    // Log but don't fail the operation if email sending fails
                    _logger.LogError(ex, "Failed to send project status change email notification: {Message}", ex.Message);
                }
            }
        }

        /// <summary>
        /// Invalidates all caches related to a project
        /// </summary>
        private async Task InvalidateProjectCaches(int projectId)
        {
            try
            {
                // Invalidate specific project cache
                string projectCacheKey = string.Format(RedisCacheKey.PROJECT_BY_ID_CACHE_KEY, projectId);
                await _cacheService.DeleteAsync(projectCacheKey);
                
                // Invalidate main project caches
                await _cacheService.DeleteAsync(RedisCacheKey.PROJECT_CACHE_KEY);
                await _cacheService.DeleteAsync(RedisCacheKey.PROJECT_LIST_CACHE_KEY);
                
                // Invalidate project status count cache
                await _cacheService.DeleteAsync(RedisCacheKey.PROJECT_STATUS_CACHE_KEY);
                
                // Clear pattern-based caches related to projects
                await _cacheService.DeleteByPatternAsync($"{RedisCacheKey.PROJECT_ALL_PATTERN}*");
            }
            catch (Exception ex)
            {
                // Log error but don't fail the operation
                Console.WriteLine($"Error invalidating project caches: {ex.Message}");
            }
        }

        public async Task<int> Delete(int id, int actionBy)
        {
            // Check if user is a Construction Manager
            if (!_helperService.IsInRole(actionBy, RoleConstValue.CONSTRUCTION_MANAGER))
            {
                throw new UnauthorizedAccessException(Message.ConstructionLogMessage.ONLY_CONSTRUCTION_MANAGER);
            }
            
            var constructionLog = await _context.ConstructionLogs.FirstOrDefaultAsync(cl => cl.Id == id && !cl.Deleted);
            
            if (constructionLog == null)
            {
                throw new KeyNotFoundException(Message.ConstructionLogMessage.NOT_FOUND);
            }
            
            // Check if user has access to the project
            var hasAccess = await _context.ProjectUsers
                .AnyAsync(pu => pu.ProjectId == constructionLog.ProjectId && pu.UserId == actionBy && !pu.Deleted);
                
            if (!hasAccess && !_helperService.IsInRole(actionBy, RoleConstValue.EXECUTIVE_BOARD))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            
            // Soft delete
            constructionLog.Deleted = true;
            constructionLog.UpdatedAt = DateTime.UtcNow;
            constructionLog.Updater = actionBy;
            
            _context.Update(constructionLog);
            await _context.SaveChangesAsync();
            
            // Invalidate related caches
            await InvalidateConstructionLogCaches(constructionLog.Id, constructionLog.ProjectId);
            
            return constructionLog.Id;
        }

        public async Task<List<ConstructionLogDTO>> List(SearchConstructionLogDTO model)
        {
            string cacheKey = $"{RedisCacheKey.CONSTRUCTION_LOG_LIST_CACHE_KEY}:{model.ProjectId}:{model.FromDate}:{model.ToDate}:{model.LogCode}:{model.LogName}:{model.TaskIndex}:{model.Status}:{model.Page}:{model.PageSize}";
            
            var cachedResult = await _cacheService.GetAsync<List<ConstructionLogDTO>>(cacheKey);
            if (cachedResult != null)
            {
                return cachedResult;
            }
            
            // Check if user has access to the project
            bool hasProjectAccess = true;
            if (model.ProjectId.HasValue)
            {
                hasProjectAccess = await _context.ProjectUsers
                    .AnyAsync(pu => pu.ProjectId == model.ProjectId && pu.UserId == model.ActionBy && !pu.Deleted);
                    
                if (!hasProjectAccess && !_helperService.IsInRole(model.ActionBy, RoleConstValue.EXECUTIVE_BOARD))
                {
                    throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
                }
            }
            else if (!_helperService.IsInRole(model.ActionBy, RoleConstValue.EXECUTIVE_BOARD))
            {
                // If no specific project is selected and not Executive Board, limit to accessible projects
                var accessibleProjects = await _context.ProjectUsers
                    .Where(pu => pu.UserId == model.ActionBy && !pu.Deleted)
                    .Select(pu => pu.ProjectId)
                    .ToListAsync();
                    
                if (!accessibleProjects.Any())
                {
                    return new List<ConstructionLogDTO>();
                }
            }
            
            // Build query
            var query = _context.ConstructionLogs
                .Where(cl => !cl.Deleted);
                
            if (model.ProjectId.HasValue)
            {
                query = query.Where(cl => cl.ProjectId == model.ProjectId);
            }
            else if (!_helperService.IsInRole(model.ActionBy, RoleConstValue.EXECUTIVE_BOARD))
            {
                // Filter for accessible projects if not Executive Board
                var accessibleProjects = await _context.ProjectUsers
                    .Where(pu => pu.UserId == model.ActionBy && !pu.Deleted)
                    .Select(pu => pu.ProjectId)
                    .ToListAsync();
                    
                query = query.Where(cl => accessibleProjects.Contains(cl.ProjectId));
            }
            
            if (!string.IsNullOrWhiteSpace(model.LogCode))
            {
                query = query.Where(cl => cl.LogCode.Contains(model.LogCode));
            }
            
            if (!string.IsNullOrWhiteSpace(model.LogName))
            {
                query = query.Where(cl => cl.LogName.Contains(model.LogName));
            }
            
            if (model.FromDate.HasValue)
            {
                query = query.Where(cl => cl.LogDate >= model.FromDate);
            }
            
            if (model.ToDate.HasValue)
            {
                query = query.Where(cl => cl.LogDate <= model.ToDate);
            }
            
            if (model.Status.HasValue)
            {
                query = query.Where(cl => cl.Status == model.Status.Value);
            }
            
            if (!string.IsNullOrWhiteSpace(model.TaskIndex))
            {
                // For task index filtering, we need to look inside the JSON data
                var constructionLogs = await query.ToListAsync();
                var filteredLogs = new List<ConstructionLog>();
                
                foreach (var log in constructionLogs)
                {
                    bool hasMatchingTask = false;
                    
                    // Check work amount for task index
                    if (log.WorkAmount != null)
                    {
                        var workAmounts = JsonSerializer.Deserialize<List<WorkAmountDTO>>(log.WorkAmount.RootElement.ToString(), DefaultSerializerOptions);
                        if (workAmounts != null && workAmounts.Any(wa => wa.TaskIndex == model.TaskIndex))
                        {
                            hasMatchingTask = true;
                        }
                    }
                    
                    // Check resources for task index
                    if (!hasMatchingTask && log.Resources != null)
                    {
                        var resources = JsonSerializer.Deserialize<List<ConstructionLogResourceDTO>>(log.Resources.RootElement.ToString(), DefaultSerializerOptions);
                        if (resources != null && resources.Any(r => r.TaskIndex == model.TaskIndex))
                        {
                            hasMatchingTask = true;
                        }
                    }
                    
                    if (hasMatchingTask)
                    {
                        filteredLogs.Add(log);
                    }
                }
                
                // Apply pagination to filtered logs
                var pagedFilteredLogs = filteredLogs
                    .OrderByDescending(cl => cl.LogDate)
                    .Skip((model.Page - 1) * model.PageSize)
                    .Take(model.PageSize)
                    .ToList();
                    
                // Map to DTOs
                var result = new List<ConstructionLogDTO>();
                foreach (var log in pagedFilteredLogs)
                {
                    result.Add(await MapToConstructionLogDTO(log));
                }
                
                // Cache the result
                await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(30));
                
                return result;
            }
            
            // Apply pagination directly to the query
            var pagedLogs = await query
                .OrderByDescending(cl => cl.LogDate)
                .Skip((model.Page - 1) * model.PageSize)
                .Take(model.PageSize)
                .ToListAsync();
                
            // Map to DTOs
            var dtoResult = new List<ConstructionLogDTO>();
            foreach (var log in pagedLogs)
            {
                dtoResult.Add(await MapToConstructionLogDTO(log));
            }
            
            // Cache the result
            await _cacheService.SetAsync(cacheKey, dtoResult, TimeSpan.FromMinutes(30));
            
            return dtoResult;
        }

        public async Task<ConstructionLogDTO> Detail(int id, int actionBy)
        {
            string cacheKey = string.Format(RedisCacheKey.CONSTRUCTION_LOG_BY_ID_CACHE_KEY, id);
            
            var cachedResult = await _cacheService.GetAsync<ConstructionLogDTO>(cacheKey);
            if (cachedResult != null)
            {
                // Check access rights for cached result
                bool hasAccess = await CheckAccessRightsForConstructionLog(cachedResult.ProjectId, actionBy);
                if (!hasAccess)
                {
                    throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
                }
                
                return cachedResult;
            }
            
            var constructionLog = await _context.ConstructionLogs.FirstOrDefaultAsync(cl => cl.Id == id && !cl.Deleted);
            
            if (constructionLog == null)
            {
                throw new KeyNotFoundException(Message.ConstructionLogMessage.NOT_FOUND);
            }
            
            // Check if user has access to the project
            bool hasAccessRights = await CheckAccessRightsForConstructionLog(constructionLog.ProjectId, actionBy);
            if (!hasAccessRights)
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            
            var result = await MapToConstructionLogDTO(constructionLog);
            
            // Cache the result
            await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromHours(1));
            
            return result;
        }

        public async Task<List<ConstructionLogDTO>> GetByProject(int projectId, int actionBy)
        {
            string cacheKey = string.Format(RedisCacheKey.CONSTRUCTION_LOG_BY_PROJECT_CACHE_KEY, projectId);
            
            var cachedResult = await _cacheService.GetAsync<List<ConstructionLogDTO>>(cacheKey);
            if (cachedResult != null)
            {
                // Check access rights
                bool hasAccess = await CheckAccessRightsForConstructionLog(projectId, actionBy);
                if (!hasAccess)
                {
                    throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
                }
                
                return cachedResult;
            }
            
            // Check if project exists
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId && !p.Deleted);
            if (project == null)
            {
                throw new KeyNotFoundException(Message.ConstructionLogMessage.PROJECT_NOT_FOUND);
            }
            
            // Check if user has access to the project
            bool hasAccessRights = await CheckAccessRightsForConstructionLog(projectId, actionBy);
            if (!hasAccessRights)
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            
            var constructionLogs = await _context.ConstructionLogs
                .Where(cl => cl.ProjectId == projectId && !cl.Deleted)
                .OrderByDescending(cl => cl.LogDate)
                .ToListAsync();
                
            var result = new List<ConstructionLogDTO>();
            foreach (var log in constructionLogs)
            {
                result.Add(await MapToConstructionLogDTO(log));
            }
            
            // Cache the result
            await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromHours(1));
            
            return result;
        }

        public async Task<ResourceLogByTaskDTO> GetResourceLogByTask(int projectId, string taskIndex, int actionBy)
        {
            string cacheKey = $"{RedisCacheKey.CONSTRUCTION_LOG_BY_TASK_CACHE_KEY}:{projectId}:{taskIndex}";
            
            var cachedResult = await _cacheService.GetAsync<ResourceLogByTaskDTO>(cacheKey);
            if (cachedResult != null)
            {
                // Check access rights
                bool hasAccess = await CheckAccessRightsForConstructionLog(projectId, actionBy);
                if (!hasAccess)
                {
                    throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
                }
                
                return cachedResult;
            }
            
            // Check if project exists
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId && !p.Deleted);
            if (project == null)
            {
                throw new KeyNotFoundException(Message.ConstructionLogMessage.PROJECT_NOT_FOUND);
            }
            
            // Check if user has access to the project
            bool hasAccessRights = await CheckAccessRightsForConstructionLog(projectId, actionBy);
            if (!hasAccessRights)
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            
            var constructionLogs = await _context.ConstructionLogs
                .Where(cl => cl.ProjectId == projectId && !cl.Deleted && cl.Status == ConstructionLogStatus.Approved)
                .OrderBy(cl => cl.LogDate)
                .ToListAsync();
                
            var result = new ResourceLogByTaskDTO
            {
                WorkAmount = new List<TaskWorkAmountDTO>(),
                Resources = new List<TaskResourceDTO>()
            };
            
            foreach (var log in constructionLogs)
            {
                // Process work amount
                if (log.WorkAmount != null)
                {
                    var workAmounts = JsonSerializer.Deserialize<List<WorkAmountDTO>>(log.WorkAmount.RootElement.ToString(), DefaultSerializerOptions);
                    var matchingWorkAmount = workAmounts?.FirstOrDefault(wa => wa.TaskIndex == taskIndex);
                    
                    if (matchingWorkAmount != null)
                    {
                        result.WorkAmount.Add(new TaskWorkAmountDTO
                        {
                            LogDate = log.LogDate.ToString("yyyy-MM-dd"),
                            WorkAmount = matchingWorkAmount.WorkAmount
                        });
                    }
                }
                
                // Process resources
                if (log.Resources != null)
                {
                    var resources = JsonSerializer.Deserialize<List<ConstructionLogResourceDTO>>(log.Resources.RootElement.ToString(), DefaultSerializerOptions);
                    var matchingResources = resources?.Where(r => r.TaskIndex == taskIndex);
                    
                    if (matchingResources != null && matchingResources.Any())
                    {
                        foreach (var resource in matchingResources)
                        {
                            // Calculate hours from start/end time
                            var startTime = resource.StartTime ?? DateTime.MinValue;
                            var endTime = resource.EndTime ?? DateTime.MinValue;
                            var hours = 0.0;
                            
                            if (startTime != DateTime.MinValue && endTime != DateTime.MinValue)
                            {
                                hours = (endTime - startTime).TotalHours;
                            }
                            
                            string unit = "giờ";
                            if (resource.ResourceType == 3) // Material
                            {
                                unit = "đơn vị"; // Default unit for materials
                            }
                            
                            result.Resources.Add(new TaskResourceDTO
                            {
                                LogDate = log.LogDate.ToString("yyyy-MM-dd"),
                                ResourceId = resource.ResourceId,
                                ResourceType = resource.ResourceType,
                                Unit = unit,
                                Quantity = resource.Quantity
                            });
                        }
                    }
                }
            }
            
            // Cache the result
            await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromHours(1));
            
            return result;
        }

        // Helper methods

        private async Task<ConstructionLogDTO> MapToConstructionLogDTO(ConstructionLog log)
        {
            var dto = new ConstructionLogDTO
            {
                Id = log.Id,
                ProjectId = log.ProjectId,
                LogCode = log.LogCode,
                LogName = log.LogName,
                LogDate = log.LogDate,
                Resources = log.Resources != null 
                    ? JsonSerializer.Deserialize<List<ConstructionLogResourceDTO>>(log.Resources.RootElement.ToString(), DefaultSerializerOptions)
                    : new List<ConstructionLogResourceDTO>(),
                WorkAmount = log.WorkAmount != null 
                    ? JsonSerializer.Deserialize<List<WorkAmountDTO>>(log.WorkAmount.RootElement.ToString(), DefaultSerializerOptions)
                    : new List<WorkAmountDTO>(),
                Weather = log.Weather != null 
                    ? JsonSerializer.Deserialize<List<WeatherDTO>>(log.Weather.RootElement.ToString(), DefaultSerializerOptions)
                    : new List<WeatherDTO>(),
                Safety = log.Safety,
                Quality = log.Quality,
                Progress = log.Progress,
                Problem = log.Problem,
                Advice = log.Advice,
                Images = log.Images != null 
                    ? JsonSerializer.Deserialize<List<AttachmentInfo>>(log.Images.RootElement.ToString(), DefaultSerializerOptions)
                    : new List<AttachmentInfo>(),
                Attachments = log.Attachments != null 
                    ? JsonSerializer.Deserialize<List<AttachmentInfo>>(log.Attachments.RootElement.ToString(), DefaultSerializerOptions)
                    : new List<AttachmentInfo>(),
                Note = log.Note,
                Status = log.Status,
                CreatedAt = log.CreatedAt ?? DateTime.MinValue,
                Creator = log.Creator,
                UpdatedAt = log.UpdatedAt ?? DateTime.MinValue,
                Updater = log.Updater,
                Deleted = log.Deleted
            };
            
            return dto;
        }

        private async Task<bool> CheckAccessRightsForConstructionLog(int projectId, int userId)
        {
            // Executive Board has access to all projects
            if (_helperService.IsInRole(userId, RoleConstValue.EXECUTIVE_BOARD))
            {
                return true;
            }
            
            // Check if user is associated with the project
            return await _context.ProjectUsers
                .AnyAsync(pu => pu.ProjectId == projectId && pu.UserId == userId && !pu.Deleted);
        }

        private async Task InvalidateConstructionLogCaches(int logId, int projectId)
        {
            // Invalidate specific cache keys
            await _cacheService.DeleteAsync(string.Format(RedisCacheKey.CONSTRUCTION_LOG_BY_ID_CACHE_KEY, logId));
            await _cacheService.DeleteAsync(string.Format(RedisCacheKey.CONSTRUCTION_LOG_BY_PROJECT_CACHE_KEY, projectId));
            
            // Invalidate pattern-based caches
            await _cacheService.DeleteByPatternAsync(RedisCacheKey.CONSTRUCTION_LOG_ALL_PATTERN);
            await _cacheService.DeleteByPatternAsync($"{RedisCacheKey.CONSTRUCTION_LOG_BY_TASK_CACHE_KEY}:{projectId}:*");
            await _cacheService.DeleteAsync(RedisCacheKey.CONSTRUCTION_LOG_CACHE_KEY);
            await _cacheService.DeleteAsync(RedisCacheKey.CONSTRUCTION_LOG_LIST_CACHE_KEY);
        }

        // Helper method to check if significant changes were made to the log
        private bool HasSignificantChanges(ConstructionLog existingLog, SaveConstructionLogDTO model)
        {
            // Check if any major fields have changed
            if (existingLog.LogName != model.LogName ||
                existingLog.LogDate != model.LogDate ||
                existingLog.Safety != model.Safety ||
                existingLog.Quality != model.Quality ||
                existingLog.Progress != model.Progress ||
                existingLog.Problem != model.Problem ||
                existingLog.Advice != model.Advice ||
                existingLog.Note != model.Note)
            {
                return true;
            }

            // Check if resources have changed
            if (model.Resources != null)
            {
                var existingResources = JsonSerializer.Deserialize<List<ConstructionLogResourceDTO>>(existingLog.Resources.RootElement.ToString(), DefaultSerializerOptions);
                if (!AreResourcesEqual(existingResources, model.Resources))
                {
                    return true;
                }
            }

            // Check if work amount has changed
            if (model.WorkAmount != null)
            {
                var existingWorkAmount = JsonSerializer.Deserialize<List<WorkAmountDTO>>(existingLog.WorkAmount.RootElement.ToString(), DefaultSerializerOptions);
                if (!AreWorkAmountsEqual(existingWorkAmount, model.WorkAmount))
                {
                    return true;
                }
            }

            // Check if weather has changed
            if (model.Weather != null)
            {
                var existingWeather = JsonSerializer.Deserialize<List<WeatherDTO>>(existingLog.Weather.RootElement.ToString(), DefaultSerializerOptions);
                if (!AreWeatherEqual(existingWeather, model.Weather))
                {
                    return true;
                }
            }

            // Check if attachments or images have changed
            if ((model.AttachmentFiles != null && model.AttachmentFiles.Any()) ||
                (model.ImageFiles != null && model.ImageFiles.Any()) ||
                model.ImageFile != null ||
                (_httpContextAccessor.HttpContext?.Request.Form.Files != null && 
                 _httpContextAccessor.HttpContext.Request.Form.Files.Any(f => f.Name == "images")))
            {
                return true;
            }

            return false;
        }

        private bool AreResourcesEqual(List<ConstructionLogResourceDTO> list1, List<ConstructionLogResourceDTO> list2)
        {
            if (list1 == null && list2 == null) return true;
            if (list1 == null || list2 == null) return false;
            if (list1.Count != list2.Count) return false;

            // Compare each resource
            for (int i = 0; i < list1.Count; i++)
            {
                var r1 = list1[i];
                var r2 = list2[i];
                
                if (r1.TaskIndex != r2.TaskIndex ||
                    r1.ResourceType != r2.ResourceType ||
                    r1.Quantity != r2.Quantity ||
                    r1.ResourceId != r2.ResourceId ||
                    r1.StartTime != r2.StartTime ||
                    r1.EndTime != r2.EndTime)
                {
                    return false;
                }
            }
            
            return true;
        }

        private bool AreWorkAmountsEqual(List<WorkAmountDTO> list1, List<WorkAmountDTO> list2)
        {
            if (list1 == null && list2 == null) return true;
            if (list1 == null || list2 == null) return false;
            if (list1.Count != list2.Count) return false;

            // Compare each work amount
            for (int i = 0; i < list1.Count; i++)
            {
                var w1 = list1[i];
                var w2 = list2[i];
                
                if (w1.TaskIndex != w2.TaskIndex ||
                    w1.WorkAmount != w2.WorkAmount)
                {
                    return false;
                }
            }
            
            return true;
        }

        private bool AreWeatherEqual(List<WeatherDTO> list1, List<WeatherDTO> list2)
        {
            if (list1 == null && list2 == null) return true;
            if (list1 == null || list2 == null) return false;
            if (list1.Count != list2.Count) return false;

            // Compare each weather entry
            for (int i = 0; i < list1.Count; i++)
            {
                var w1 = list1[i];
                var w2 = list2[i];
                
                if (w1.Type != w2.Type)
                {
                    return false;
                }
                
                // Compare values
                if (w1.Values.Count != w2.Values.Count)
                {
                    return false;
                }
                
                for (int j = 0; j < w1.Values.Count; j++)
                {
                    if (w1.Values[j] != w2.Values[j])
                    {
                        return false;
                    }
                }
            }
            
            return true;
        }

        /// <summary>
        /// Invalidates all caches related to ConstructionProgress
        /// </summary>
        private async Task InvalidateConstructionProgressCaches(int progressId, int projectId, int planId)
        {
            // Invalidate specific cache keys
            await _cacheService.DeleteAsync(string.Format(RedisCacheKey.CONSTRUCTION_PROGRESS_BY_ID_CACHE_KEY, progressId));
            await _cacheService.DeleteAsync(string.Format(RedisCacheKey.CONSTRUCTION_PROGRESS_BY_PROJECT_CACHE_KEY, projectId));
            await _cacheService.DeleteAsync(string.Format(RedisCacheKey.CONSTRUCTION_PROGRESS_BY_PLAN_CACHE_KEY, planId));
            
            // Invalidate general cache keys
            await _cacheService.DeleteAsync(RedisCacheKey.CONSTRUCTION_PROGRESS_CACHE_KEY);
            
            // Invalidate pattern-based caches
            await _cacheService.DeleteByPatternAsync(RedisCacheKey.CONSTRUCTION_PROGRESS_ALL_PATTERN);
        }

        /// <summary>
        /// Invalidates all caches that could be affected when a construction log is approved
        /// </summary>
        /// <param name="logId">The ID of the construction log that was approved</param>
        /// <param name="projectId">The ID of the project the log belongs to</param>
        /// <returns>Async task</returns>
        private async Task InvalidateAllRelatedCaches(int logId, int projectId)
        {
            try
            {
                _logger.LogInformation($"Invalidating all related caches for construction log {logId} in project {projectId}");
                
                // 1. Invalidate construction log caches
                await InvalidateConstructionLogCaches(logId, projectId);
                
                // 2. Get construction progress for this project to invalidate related caches
                var constructionProgress = await _context.ConstructionProgresses
                    .FirstOrDefaultAsync(cp => cp.ProjectId == projectId && !cp.Deleted);
                
                if (constructionProgress != null)
                {
                    // 3. Invalidate construction progress caches
                    await InvalidateConstructionProgressCaches(constructionProgress.Id, projectId, constructionProgress.PlanId);
                    
                    // 4. Invalidate construction progress item caches - no specific constants for these, use patterns
                    await _cacheService.DeleteByPatternAsync("ConstructionProgressItem:*");
                    await _cacheService.DeleteByPatternAsync($"ConstructionProgressItem:Project:{projectId}:*");
                    
                    // 5. Invalidate construction plan caches
                    await _cacheService.DeleteAsync(RedisCacheKey.CONSTRUCTION_PLAN_CACHE_KEY);
                    await _cacheService.DeleteByPatternAsync($"CONSTRUCTION_PLAN:*");
                }
                
                // 6. Invalidate inspection report caches as they depend on construction progress status
                await _cacheService.DeleteAsync(RedisCacheKey.INSPECTION_REPORT_CACHE_KEY);
                await _cacheService.DeleteAsync(string.Format(RedisCacheKey.INSPECTION_REPORT_BY_PROJECT_CACHE_KEY, projectId));
                await _cacheService.DeleteByPatternAsync(RedisCacheKey.INSPECTION_REPORT_LIST_CACHE_KEY + "*");
                await _cacheService.DeleteByPatternAsync(RedisCacheKey.INSPECTION_REPORT_ALL_PATTERN);
                
                // 7. Invalidate project caches
                await InvalidateProjectCaches(projectId);
                
                // 8. Invalidate resource inventory caches
                await _cacheService.DeleteAsync(RedisCacheKey.RESOURCE_INVENTORY_CACHE_KEY);
                await _cacheService.DeleteByPatternAsync($"RESOURCE_INVENTORY:*");
                await _cacheService.DeleteAsync(string.Format(RedisCacheKey.RESOURCE_INVENTORY_BY_TYPE_CACHE_KEY, "*"));
                
                // 9. Invalidate task-related caches (using pattern as there's no dedicated constant)
                await _cacheService.DeleteByPatternAsync($"TASK:*");
                await _cacheService.DeleteByPatternAsync($"TASK:PROJECT:{projectId}:*");
                
                // 10. Invalidate dashboard/statistics/report caches (using pattern as there's no dedicated constant)
                await _cacheService.DeleteByPatternAsync($"DASHBOARD:*");
                await _cacheService.DeleteByPatternAsync($"STATISTICS:*");
                await _cacheService.DeleteByPatternAsync($"REPORT:*");
                
                // 11. Invalidate project statistics cache
                await _cacheService.DeleteAsync(string.Format(RedisCacheKey.PROJECT_STATISTICS_PROJECT_CACHE_KEY, projectId));
                
                // 12. Invalidate user-related caches for project members
                var projectUserIds = await _context.ProjectUsers
                    .Where(pu => pu.ProjectId == projectId && !pu.Deleted)
                    .Select(pu => pu.UserId)
                    .ToListAsync();
                
                // Also invalidate project user cache
                await _cacheService.DeleteAsync(RedisCacheKey.PROJECT_USER_CACHE_KEY);
                
                foreach (var userId in projectUserIds)
                {
                    await _cacheService.DeleteByPatternAsync($"USER:{userId}:*");
                    await _cacheService.DeleteByPatternAsync($"NOTIFICATIONS:USER:{userId}:*");
                    await _cacheService.DeleteAsync(string.Format(RedisCacheKey.PROJECT_STATISTICS_USER_CACHE_KEY, userId));
                }
                
                _logger.LogInformation($"Cache invalidation completed for construction log {logId} approval");
            }
            catch (Exception ex)
            {
                // Log error but don't fail the operation
                _logger.LogError(ex, $"Error during cache invalidation after construction log approval: {ex.Message}");
            }
        }
    }
} 