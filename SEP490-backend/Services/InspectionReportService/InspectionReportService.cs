using Microsoft.EntityFrameworkCore;
using Sep490_Backend.DTO;
using Sep490_Backend.DTO.InspectionReport;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Infra.Enums;
using Sep490_Backend.Services.CacheService;
using Sep490_Backend.Services.HelperService;
using Sep490_Backend.Services.GoogleDriveService;
using Sep490_Backend.Services.ConstructionProgressService;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Sep490_Backend.Services.ProjectService;

namespace Sep490_Backend.Services.InspectionReportService
{
    public class InspectionReportService : IInspectionReportService
    {
        private readonly BackendContext _context;
        private readonly ICacheService _cacheService;
        private readonly IHelperService _helperService;
        private readonly IGoogleDriveService _googleDriveService;
        private readonly ILogger<InspectionReportService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public InspectionReportService(
            BackendContext context,
            ICacheService cacheService,
            IHelperService helperService,
            IGoogleDriveService googleDriveService,
            ILogger<InspectionReportService> logger,
            IServiceProvider serviceProvider)
        {
            _context = context;
            _cacheService = cacheService;
            _helperService = helperService;
            _googleDriveService = googleDriveService;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task<InspectionReportDTO> Save(SaveInspectionReportDTO model, int actionBy)
        {
            // Check authorization - both Quality Assurance and Executive Board can create/update reports
            if (!_helperService.IsInRole(actionBy, RoleConstValue.QUALITY_ASSURANCE) && 
                !_helperService.IsInRole(actionBy, RoleConstValue.EXECUTIVE_BOARD))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // If only updating the status (approve/reject), handle specially
            if (model.Id != 0 && model.Status.HasValue && 
                (model.ConstructionProgressItemId == 0 && model.InspectStartDate == default))
            {
                var result = await UpdateInspectionReportStatus(model.Id, model.Status.Value, actionBy);
                
                // Email notification is already handled in UpdateInspectionReportStatus
                
                return result;
            }

            // Check if progress item exists
            var progressItem = await _context.ConstructionProgressItems
                .Include(pi => pi.ConstructionProgress)
                .ThenInclude(cp => cp.Project)
                .FirstOrDefaultAsync(pi => pi.Id == model.ConstructionProgressItemId && !pi.Deleted);
                
            if (progressItem == null)
            {
                throw new KeyNotFoundException(Message.InspectionReportMessage.NOT_FOUND);
            }

            var projectId = progressItem.ConstructionProgress.ProjectId;

            // Check if user has access to the project
            var hasAccess = await _context.ProjectUsers
                .AnyAsync(pu => pu.ProjectId == projectId && pu.UserId == actionBy && !pu.Deleted);
                
            if (!hasAccess && !_helperService.IsInRole(actionBy, RoleConstValue.EXECUTIVE_BOARD))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Generate inspection code based on Project Code
            string inspectCode;
            if (model.Id == 0) // Creating new report
            {
                var project = progressItem.ConstructionProgress.Project;
                
                // Get the latest index for this project
                int nextIndex = 1;
                var existingReports = await _context.Set<InspectionReport>()
                    .Where(ir => ir.ConstructionProgressItem.ConstructionProgress.ProjectId == projectId && !ir.Deleted)
                    .ToListAsync();
                
                if (existingReports.Any())
                {
                    // Extract indices from existing codes that match the pattern
                    var pattern = $"{project.ProjectCode}_IR_";
                    var indices = existingReports
                        .Where(ir => ir.InspectCode.StartsWith(pattern))
                        .Select(ir => 
                        {
                            if (int.TryParse(ir.InspectCode.Substring(pattern.Length), out int index))
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
                
                inspectCode = $"{project.ProjectCode}_IR_{nextIndex}";
            }
            else // Updating existing report
            {
                var existingReport = await _context.Set<InspectionReport>().FirstOrDefaultAsync(ir => ir.Id == model.Id && !ir.Deleted);
                
                if (existingReport == null)
                {
                    throw new KeyNotFoundException(Message.InspectionReportMessage.NOT_FOUND);
                }
                
                // Keep existing code
                inspectCode = existingReport.InspectCode;
            }

            // Handle file attachments - Replaced with improved implementation
            List<AttachmentInfo> attachmentInfos = new List<AttachmentInfo>();
            
            // Get existing attachments if updating
            if (model.Id != 0)
            {
                var existingReport = await _context.Set<InspectionReport>().FirstOrDefaultAsync(ir => ir.Id == model.Id && !ir.Deleted);
                if (existingReport?.Attachment != null)
                {
                    attachmentInfos = JsonSerializer.Deserialize<List<AttachmentInfo>>(existingReport.Attachment.RootElement.ToString());
                    
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
                            _logger.LogError($"Failed to delete old attachments: {ex.Message}");
                        }
                    }
                }
            }

            // Upload new attachments if any
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
                            WebContentLink = uploadResult,
                            MimeType = file.ContentType
                        });
                    }
                }
            }

            // Convert attachments to JsonDocument - this step was missing before
            JsonDocument? attachmentsJson = null;
            if (attachmentInfos.Any())
            {
                attachmentsJson = JsonDocument.Parse(JsonSerializer.Serialize(attachmentInfos));
            }

            // Create or update the inspection report
            InspectionReport inspectionReport;
            
            if (model.Id == 0)
            {
                // Create new inspection report
                inspectionReport = new InspectionReport
                {
                    ConstructionProgressItemId = model.ConstructionProgressItemId,
                    InspectCode = inspectCode,
                    InspectorId = model.InspectorId,
                    InspectStartDate = model.InspectStartDate,
                    InspectEndDate = model.InspectEndDate,
                    Location = model.Location,
                    InspectionName = model.InspectionName,
                    Attachment = attachmentsJson, // Set the JsonDocument
                    InspectionDecision = (int)(model.InspectionDecision ?? InspectionDecision.None),
                    Status = (int)(model.Status ?? InspectionReportStatus.Draft),
                    QualityNote = model.QualityNote,
                    OtherNote = model.OtherNote,
                    Creator = actionBy,
                    Updater = actionBy
                };
                
                _context.Set<InspectionReport>().Add(inspectionReport);
                
                // Update the associated ConstructionProgressItem only if:
                // 1. InspectionDecision is Pass
                // 2. Status is Approved
                if (model.InspectionDecision == InspectionDecision.Pass && 
                    model.Status == InspectionReportStatus.Approved)
                {
                    // Record the old progress value
                    bool becameCompleted = progressItem.Progress < 100;
                    
                    // Set its progress to 100% and status to Done
                    progressItem.Progress = 100;
                    progressItem.Status = ProgressStatusEnum.Done;
                    progressItem.Updater = actionBy;
                    progressItem.UpdatedAt = DateTime.Now;
                    progressItem.ActualEndDate = DateTime.Now;
                    
                    _context.ConstructionProgressItems.Update(progressItem);
                    
                    // Rollback materials from the progress item to resource inventory
                    await RollbackMaterials(progressItem, actionBy);
                    
                    // Update parent progress calculation if needed
                    if (!string.IsNullOrEmpty(progressItem.ParentIndex))
                    {
                        _logger.LogInformation($"Progress item {progressItem.Id} has parent index {progressItem.ParentIndex}, checking siblings status");
                        
                        // Get all progress items for this progress to calculate parent progress
                        var progressItems = await _context.ConstructionProgressItems
                            .Where(pi => pi.ProgressId == progressItem.ProgressId && !pi.Deleted)
                            .ToListAsync();
                        
                        // Create a set of indices to update
                        var updatedItemIndices = new HashSet<string> { progressItem.Index };
                        
                        // Add parent index
                        updatedItemIndices.Add(progressItem.ParentIndex);
                        
                        // Check if all siblings (children of parent) are in Done status
                        var parentItem = progressItems.FirstOrDefault(pi => pi.Index == progressItem.ParentIndex);
                        if (parentItem != null)
                        {
                            var siblingItems = progressItems
                                .Where(pi => pi.ParentIndex == progressItem.ParentIndex && !pi.Deleted)
                                .ToList();
                                
                            bool allSiblingsDone = siblingItems.All(si => si.Status == ProgressStatusEnum.Done);
                            
                            if (allSiblingsDone)
                            {
                                _logger.LogInformation($"All siblings of progress item {progressItem.Id} are Done, setting parent {parentItem.Id} to Done");
                                
                                // Set parent to Done
                                parentItem.Progress = 100;
                                parentItem.Status = ProgressStatusEnum.Done;
                                parentItem.UpdatedAt = DateTime.Now;
                                parentItem.Updater = actionBy;
                                
                                _context.ConstructionProgressItems.Update(parentItem);
                                
                                // If this parent also has a parent, add its parent to the updated indices
                                if (!string.IsNullOrEmpty(parentItem.ParentIndex))
                                {
                                    updatedItemIndices.Add(parentItem.ParentIndex);
                                }
                            }
                        }
                        
                        // Update progress of parent items based on their children
                        await UpdateParentItemsProgress(progressItems, updatedItemIndices, actionBy);
                        
                        // Ensure all parent progress updates are saved to the database
                        await _context.SaveChangesAsync();
                    }
                    
                    // Check if all progress items for the project are now Done, and if so,
                    // update the project status to WaitingApproveCompleted
                    var relatedProjectId = progressItem.ConstructionProgress.ProjectId;
                    
                    // Use the helper method to check if all progress items are done and update status if needed
                    await SetProjectStatusToWaitingApproveCompleted(relatedProjectId, actionBy);
                }
            }
            else
            {
                // Update existing inspection report
                inspectionReport = await _context.Set<InspectionReport>().FirstOrDefaultAsync(ir => ir.Id == model.Id && !ir.Deleted);
                
                if (inspectionReport == null)
                {
                    throw new KeyNotFoundException(Message.InspectionReportMessage.NOT_FOUND);
                }
                
                inspectionReport.ConstructionProgressItemId = model.ConstructionProgressItemId;
                inspectionReport.InspectorId = model.InspectorId;
                inspectionReport.InspectStartDate = model.InspectStartDate;
                inspectionReport.InspectEndDate = model.InspectEndDate;
                inspectionReport.Location = model.Location;
                inspectionReport.InspectionName = model.InspectionName;
                inspectionReport.Attachment = attachmentsJson; // Set the JsonDocument
                
                if (model.InspectionDecision.HasValue)
                {
                    inspectionReport.InspectionDecision = (int)model.InspectionDecision.Value;
                }
                
                if (model.Status.HasValue)
                {
                    inspectionReport.Status = (int)model.Status.Value;
                }
                
                inspectionReport.QualityNote = model.QualityNote;
                inspectionReport.OtherNote = model.OtherNote;
                inspectionReport.Updater = actionBy;
                
                _context.Set<InspectionReport>().Update(inspectionReport);
            }
            
            await _context.SaveChangesAsync();
            
            // Invalidate caches
            await InvalidateInspectionReportCaches(inspectionReport.Id, projectId);
            
            // Invalidate construction progress caches
            var progressId = progressItem.ConstructionProgress.Id;
            var planId = progressItem.ConstructionProgress.PlanId;
            
            // Use comprehensive cache invalidation if this is a new Approved report or status changed to Approved
            if (model.Status == InspectionReportStatus.Approved && 
                (model.Id == 0 || model.InspectionDecision.HasValue))
            {
                await InvalidateAllRelatedCaches(inspectionReport.Id, projectId, progressId, planId);
            }
            else
            {
                // For non-approved reports, just invalidate basic caches
                await InvalidateProgressCache(progressId, projectId, planId);
            }
            
            // Return the updated data with additional details
            return await MapToInspectionReportDTO(inspectionReport);
        }

        private async Task<InspectionReportDTO> UpdateInspectionReportStatus(int id, InspectionReportStatus status, int actionBy)
        {
            // Check if report exists
            var report = await _context.Set<InspectionReport>()
                .Include(ir => ir.ConstructionProgressItem)
                .ThenInclude(cpi => cpi.ConstructionProgress)
                .FirstOrDefaultAsync(ir => ir.Id == id && !ir.Deleted);
            
            if (report == null)
            {
                throw new KeyNotFoundException(Message.InspectionReportMessage.NOT_FOUND);
            }
            
            // Check authorization - both Quality Assurance users and Executive Board members can update report statuses
            if (!_helperService.IsInRole(actionBy, RoleConstValue.QUALITY_ASSURANCE) && 
                !_helperService.IsInRole(actionBy, RoleConstValue.EXECUTIVE_BOARD))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            
            // Update status
            report.Status = (int)status;
            report.Updater = actionBy;
            
            _context.Set<InspectionReport>().Update(report);
            
            // Track if progress item was updated to invalidate related caches later
            bool progressItemUpdated = false;
            int projectId = report.ConstructionProgressItem.ConstructionProgress.ProjectId;
            var progressId = report.ConstructionProgressItem.ConstructionProgress.Id;
            var planId = report.ConstructionProgressItem.ConstructionProgress.PlanId;

            // If status is being set to Approved and the inspection decision is Pass,
            // update the construction progress item status and progress
            if (status == InspectionReportStatus.Approved)
            {
                var progressItem = await _context.ConstructionProgressItems
                    .Include(pi => pi.ConstructionProgress)
                    .Include(pi => pi.Details)
                    .FirstOrDefaultAsync(pi => pi.Id == report.ConstructionProgressItemId && !pi.Deleted);
                
                if (progressItem != null)
                {
                    progressItemUpdated = true;
                    
                    if ((InspectionDecision)report.InspectionDecision == InspectionDecision.Pass)
                    {
                        // Record the old progress value
                        bool becameCompleted = progressItem.Progress < 100;
                        
                        // Update progress item status
                        progressItem.Progress = 100;
                        progressItem.Status = ProgressStatusEnum.Done;
                        progressItem.Updater = actionBy;
                        progressItem.UpdatedAt = DateTime.Now;
                        progressItem.ActualEndDate = DateTime.Now;
                        
                        _context.ConstructionProgressItems.Update(progressItem);

                        // Rollback materials from the progress item to resource inventory
                        await RollbackMaterials(progressItem, actionBy);
                        
                        // Update parent progress calculation if needed
                        if (!string.IsNullOrEmpty(progressItem.ParentIndex))
                        {
                            _logger.LogInformation($"Progress item {progressItem.Id} has parent index {progressItem.ParentIndex}, checking siblings status");
                            
                            // Get all progress items for this progress to calculate parent progress
                            var progressItems = await _context.ConstructionProgressItems
                                .Where(pi => pi.ProgressId == progressItem.ProgressId && !pi.Deleted)
                                .ToListAsync();
                            
                            // Create a set of indices to update
                            var updatedItemIndices = new HashSet<string> { progressItem.Index };
                            
                            // Add parent index
                            updatedItemIndices.Add(progressItem.ParentIndex);
                            
                            // Check if all siblings (children of parent) are in Done status
                            var parentItem = progressItems.FirstOrDefault(pi => pi.Index == progressItem.ParentIndex);
                            if (parentItem != null)
                            {
                                var siblingItems = progressItems
                                    .Where(pi => pi.ParentIndex == progressItem.ParentIndex && !pi.Deleted)
                                    .ToList();
                                    
                                bool allSiblingsDone = siblingItems.All(si => si.Status == ProgressStatusEnum.Done);
                                
                                if (allSiblingsDone)
                                {
                                    _logger.LogInformation($"All siblings of progress item {progressItem.Id} are Done, setting parent {parentItem.Id} to Done");
                                    
                                    // Set parent to Done
                                    parentItem.Progress = 100;
                                    parentItem.Status = ProgressStatusEnum.Done;
                                    parentItem.UpdatedAt = DateTime.Now;
                                    parentItem.Updater = actionBy;
                                    
                                    _context.ConstructionProgressItems.Update(parentItem);
                                    
                                    // If this parent also has a parent, add its parent to the updated indices
                                    if (!string.IsNullOrEmpty(parentItem.ParentIndex))
                                    {
                                        updatedItemIndices.Add(parentItem.ParentIndex);
                                    }
                                }
                            }
                            
                            // Update progress of parent items based on their children
                            await UpdateParentItemsProgress(progressItems, updatedItemIndices, actionBy);
                            
                            // Ensure all parent progress updates are saved to the database
                            await _context.SaveChangesAsync();
                        }
                        
                        // Check if all progress items for the project are now Done, and if so,
                        // update the project status to WaitingApproveCompleted
                        var relatedProjectId = progressItem.ConstructionProgress.ProjectId;
                        
                        // Use the helper method to check if all progress items are done and update status if needed
                        await SetProjectStatusToWaitingApproveCompleted(relatedProjectId, actionBy);
                    }
                    else if ((InspectionDecision)report.InspectionDecision == InspectionDecision.Fail)
                    {
                        // Change progress item status to InspectionFailed when failed inspection is approved
                        progressItem.Status = ProgressStatusEnum.InspectionFailed;
                        progressItem.Updater = actionBy;
                        progressItem.UpdatedAt = DateTime.Now;
                        
                        _context.ConstructionProgressItems.Update(progressItem);
                        
                        _logger.LogInformation($"Progress item {progressItem.Id} marked as InspectionFailed due to failed inspection report {report.Id}");
                        
                        // Update parent progress calculation if needed
                        if (!string.IsNullOrEmpty(progressItem.ParentIndex))
                        {
                            // Get all progress items for this progress to calculate parent progress
                            var progressItems = await _context.ConstructionProgressItems
                                .Where(pi => pi.ProgressId == progressItem.ProgressId && !pi.Deleted)
                                .ToListAsync();
                            
                            // Create a set of indices to update
                            var updatedItemIndices = new HashSet<string> { progressItem.Index };
                            
                            // Add parent index
                            updatedItemIndices.Add(progressItem.ParentIndex);
                            
                            // Update progress of parent items based on their children
                            await UpdateParentItemsProgress(progressItems, updatedItemIndices, actionBy);
                        }
                    }
                }
            }
            
            await _context.SaveChangesAsync();
            
            // Send email notification
            try
            {
                var emailService = _serviceProvider.GetService<IInspectionReportEmailService>();
                if (emailService != null)
                {
                    await emailService.SendInspectionReportStatusNotification(report.Id, status, actionBy);
                    _logger.LogInformation("Email notification queued for inspection report {ReportId} status change to {Status}", 
                        report.Id, status);
                }
            }
            catch (Exception ex)
            {
                // Log the error but don't fail the operation
                _logger.LogError(ex, "Failed to send email notification for inspection report {ReportId}", report.Id);
            }
            
            // Perform comprehensive cache invalidation if progress item was updated
            if (progressItemUpdated && status == InspectionReportStatus.Approved)
            {
                await InvalidateAllRelatedCaches(report.Id, projectId, progressId, planId);
            }
            else
            {
                // For non-approval status changes or when no progress items are affected, 
                // just invalidate inspection report caches
                await InvalidateInspectionReportCaches(report.Id, projectId);
            }
            
            // Return updated report
            return await MapToInspectionReportDTO(report);
        }

        /// <summary>
        /// Invalidates all caches that could be affected when an inspection report is approved and updates progress items
        /// </summary>
        /// <param name="reportId">The ID of the inspection report that was approved</param>
        /// <param name="projectId">The ID of the project</param>
        /// <param name="progressId">The ID of the construction progress</param>
        /// <param name="planId">The ID of the construction plan</param>
        /// <returns>Task</returns>
        private async Task InvalidateAllRelatedCaches(int reportId, int projectId, int progressId, int planId)
        {
            try
            {
                _logger.LogInformation($"Invalidating all related caches for inspection report {reportId} in project {projectId}");
                
                // 1. Invalidate inspection report caches
                await InvalidateInspectionReportCaches(reportId, projectId);
                
                // 2. Invalidate construction progress caches
                await InvalidateProgressCache(progressId, projectId, planId);
                
                // 3. Invalidate construction progress item caches - no specific constants for these, use patterns
                await _cacheService.DeleteByPatternAsync("ConstructionProgressItem:*");
                await _cacheService.DeleteByPatternAsync($"ConstructionProgressItem:Project:{projectId}:*");
                
                // 4. Invalidate construction plan caches
                await _cacheService.DeleteAsync(RedisCacheKey.CONSTRUCTION_PLAN_CACHE_KEY);
                await _cacheService.DeleteByPatternAsync($"CONSTRUCTION_PLAN:*");
                
                // 5. Invalidate project caches more comprehensively
                await InvalidateProjectCaches(projectId);
                await _cacheService.DeleteByPatternAsync($"{RedisCacheKey.PROJECT_ALL_PATTERN}*");
                
                // 6. Invalidate resource inventory caches
                await _cacheService.DeleteAsync(RedisCacheKey.RESOURCE_INVENTORY_CACHE_KEY);
                await _cacheService.DeleteByPatternAsync($"RESOURCE_INVENTORY:*");
                await _cacheService.DeleteAsync(string.Format(RedisCacheKey.RESOURCE_INVENTORY_BY_TYPE_CACHE_KEY, "*"));
                await _cacheService.DeleteByPatternAsync($"RESOURCE_INVENTORY:PROJECT:{projectId}:*");
                
                // 7. Invalidate vehicle caches (affected by RollbackMaterials method)
                await _cacheService.DeleteByPatternAsync($"VEHICLE:*");
                await _cacheService.DeleteByPatternAsync($"MACHINE:*");
                
                // 8. Invalidate construction log caches (they display progress data)
                await _cacheService.DeleteAsync(RedisCacheKey.CONSTRUCTION_LOG_CACHE_KEY);
                await _cacheService.DeleteAsync(string.Format(RedisCacheKey.CONSTRUCTION_LOG_BY_PROJECT_CACHE_KEY, projectId));
                await _cacheService.DeleteByPatternAsync(RedisCacheKey.CONSTRUCTION_LOG_ALL_PATTERN);
                
                // 9. Invalidate task-related caches (using pattern as there's no dedicated constant)
                await _cacheService.DeleteByPatternAsync($"TASK:*");
                await _cacheService.DeleteByPatternAsync($"TASK:PROJECT:{projectId}:*");
                
                // 10. Invalidate dashboard/statistics/report caches
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
                
                _logger.LogInformation($"Cache invalidation completed for inspection report {reportId} approval");
            }
            catch (Exception ex)
            {
                // Log error but don't fail the operation
                _logger.LogError(ex, $"Error during cache invalidation after inspection report approval: {ex.Message}");
            }
        }

        public async Task<int> Delete(int id, int actionBy)
        {
            // Check if user is authorized to delete reports - both Quality Assurance and Executive Board can delete
            if (!_helperService.IsInRole(actionBy, RoleConstValue.QUALITY_ASSURANCE) && 
                !_helperService.IsInRole(actionBy, RoleConstValue.EXECUTIVE_BOARD))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            
            // Check if report exists
            var report = await _context.Set<InspectionReport>()
                .Include(ir => ir.ConstructionProgressItem)
                .ThenInclude(cpi => cpi.ConstructionProgress)
                .FirstOrDefaultAsync(ir => ir.Id == id && !ir.Deleted);
            
            if (report == null)
            {
                throw new KeyNotFoundException(Message.InspectionReportMessage.NOT_FOUND);
            }
            
            int projectId = report.ConstructionProgressItem.ConstructionProgress.ProjectId;
            
            // Check if user has access to the project - skip this check for Executive Board
            if (!_helperService.IsInRole(actionBy, RoleConstValue.EXECUTIVE_BOARD))
            {
                var hasAccess = await _context.ProjectUsers
                    .AnyAsync(pu => pu.ProjectId == projectId && pu.UserId == actionBy && !pu.Deleted);
                    
                if (!hasAccess)
                {
                    throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
                }
            }
            
            // Soft delete the report
            report.Deleted = true;
            report.Updater = actionBy;
            
            _context.Set<InspectionReport>().Update(report);
            await _context.SaveChangesAsync();
            
            // Invalidate cache
            await InvalidateInspectionReportCaches(report.Id, projectId);
            
            return id;
        }

        public async Task<List<InspectionReportDTO>> List(SearchInspectionReportDTO model)
        {
            // Generate a unique cache key based on search parameters
            string cacheKey = $"{RedisCacheKey.INSPECTION_REPORT_LIST_CACHE_KEY}:{model.ProjectId ?? 0}:{model.InspectorId ?? 0}:{model.ConstructionProgressItemId ?? 0}:{model.StartDate?.ToString("yyyyMMdd") ?? "0"}:{model.EndDate?.ToString("yyyyMMdd") ?? "0"}:{model.Status ?? 0}:{model.Decision ?? 0}:{model.Keyword ?? "none"}:{model.PageIndex}:{model.PageSize}";
            
            var cachedResult = await _cacheService.GetAsync<List<InspectionReportDTO>>(cacheKey);
            
            if (cachedResult != null)
            {
                return cachedResult;
            }
            
            // Create query
            var query = _context.Set<InspectionReport>()
                .Include(ir => ir.Inspector)
                .Include(ir => ir.ConstructionProgressItem)
                .ThenInclude(cpi => cpi.ConstructionProgress)
                .ThenInclude(cp => cp.Project)
                .Where(ir => !ir.Deleted)
                .AsQueryable();
            
            // Apply filters
            if (model.ProjectId.HasValue)
            {
                query = query.Where(ir => ir.ConstructionProgressItem.ConstructionProgress.ProjectId == model.ProjectId.Value);
            }
            
            if (model.InspectorId.HasValue)
            {
                query = query.Where(ir => ir.InspectorId == model.InspectorId.Value);
            }
            
            if (model.ConstructionProgressItemId.HasValue)
            {
                query = query.Where(ir => ir.ConstructionProgressItemId == model.ConstructionProgressItemId.Value);
            }
            
            if (model.StartDate.HasValue)
            {
                query = query.Where(ir => ir.InspectStartDate >= model.StartDate.Value);
            }
            
            if (model.EndDate.HasValue)
            {
                query = query.Where(ir => ir.InspectEndDate <= model.EndDate.Value);
            }
            
            if (model.Status.HasValue)
            {
                query = query.Where(ir => ir.Status == (int)model.Status.Value);
            }
            
            if (model.Decision.HasValue)
            {
                query = query.Where(ir => ir.InspectionDecision == (int)model.Decision.Value);
            }
            
            if (!string.IsNullOrEmpty(model.Keyword))
            {
                query = query.Where(ir => ir.InspectCode.Contains(model.Keyword) ||
                                          ir.Location.Contains(model.Keyword) ||
                                          ir.InspectionName.Contains(model.Keyword) ||
                                          (ir.QualityNote != null && ir.QualityNote.Contains(model.Keyword)) ||
                                          (ir.OtherNote != null && ir.OtherNote.Contains(model.Keyword)));
            }
            
            // Get total count for pagination
            model.Total = await query.CountAsync();
            
            // Apply pagination
            var results = await query
                .OrderByDescending(ir => ir.CreatedAt)
                .Skip(model.Skip)
                .Take(model.PageSize)
                .ToListAsync();
            
            // Map to DTOs
            var dtos = new List<InspectionReportDTO>();
            foreach (var item in results)
            {
                dtos.Add(await MapToInspectionReportDTO(item));
            }
            
            // Store in cache (5 minute expiry)
            await _cacheService.SetAsync(cacheKey, dtos, TimeSpan.FromMinutes(5));
            
            return dtos;
        }

        public async Task<InspectionReportDTO> Detail(int id, int actionBy)
        {
            // Check if report exists
            var report = await _context.Set<InspectionReport>()
                .Include(ir => ir.Inspector)
                .Include(ir => ir.ConstructionProgressItem)
                .ThenInclude(cpi => cpi.ConstructionProgress)
                .ThenInclude(cp => cp.Project)
                .FirstOrDefaultAsync(ir => ir.Id == id && !ir.Deleted);
            
            if (report == null)
            {
                throw new KeyNotFoundException(Message.InspectionReportMessage.NOT_FOUND);
            }
            
            int projectId = report.ConstructionProgressItem.ConstructionProgress.ProjectId;
            
            // Check if user has access to the project
            var hasAccess = await _context.ProjectUsers
                .AnyAsync(pu => pu.ProjectId == projectId && pu.UserId == actionBy && !pu.Deleted);
                
            if (!hasAccess && !_helperService.IsInRole(actionBy, RoleConstValue.EXECUTIVE_BOARD))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            
            // Map to DTO and return
            return await MapToInspectionReportDTO(report);
        }

        public async Task<List<InspectionReportDTO>> GetByProject(int projectId, int actionBy)
        {
            // Check if project exists
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId && !p.Deleted);
            if (project == null)
            {
                throw new KeyNotFoundException(Message.InspectionReportMessage.PROJECT_NOT_FOUND);
            }
            
            // Check if user has access to the project
            var hasAccess = await _context.ProjectUsers
                .AnyAsync(pu => pu.ProjectId == projectId && pu.UserId == actionBy && !pu.Deleted);
                
            if (!hasAccess && !_helperService.IsInRole(actionBy, RoleConstValue.EXECUTIVE_BOARD))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            
            // Get from cache or database
            string cacheKey = string.Format(RedisCacheKey.INSPECTION_REPORT_BY_PROJECT_CACHE_KEY, projectId);
            
            try
            {
                var cachedResult = await _cacheService.GetAsync<List<InspectionReportDTO>>(cacheKey);
                
                if (cachedResult != null)
                {
                    return cachedResult;
                }
            }
            catch (Exception ex)
            {
                // Log cache retrieval error but continue to fetch from database
                Console.WriteLine($"Error retrieving inspection reports from cache: {ex.Message}");
            }
            
            // Query reports for this project
            var reports = await _context.Set<InspectionReport>()
                .Include(ir => ir.Inspector)
                .Include(ir => ir.ConstructionProgressItem)
                .ThenInclude(cpi => cpi.ConstructionProgress)
                .ThenInclude(cp => cp.Project)
                .Where(ir => ir.ConstructionProgressItem.ConstructionProgress.ProjectId == projectId && !ir.Deleted)
                .OrderByDescending(ir => ir.CreatedAt)
                .ToListAsync();
            
            // Map to DTOs
            var dtos = new List<InspectionReportDTO>();
            foreach (var item in reports)
            {
                dtos.Add(await MapToInspectionReportDTO(item));
            }
            
            // Store in cache (5 minute expiry)
            await _cacheService.SetAsync(cacheKey, dtos, TimeSpan.FromMinutes(5));
            
            return dtos;
        }

        private async Task<InspectionReportDTO> MapToInspectionReportDTO(InspectionReport report)
        {
            var project = await _context.Projects
                .FirstOrDefaultAsync(p => p.Id == report.ConstructionProgressItem.ConstructionProgress.ProjectId && !p.Deleted);
                
            var dto = new InspectionReportDTO
            {
                Id = report.Id,
                ConstructionProgressItemId = report.ConstructionProgressItemId,
                ProgressItemName = report.ConstructionProgressItem?.WorkName,
                ProjectName = project?.ProjectName,
                InspectCode = report.InspectCode,
                InspectorId = report.InspectorId,
                InspectorName = report.Inspector?.FullName,
                InspectStartDate = report.InspectStartDate,
                InspectEndDate = report.InspectEndDate,
                Location = report.Location,
                InspectionName = report.InspectionName,
                InspectionDecision = (InspectionDecision)report.InspectionDecision,
                Status = (InspectionReportStatus)report.Status,
                QualityNote = report.QualityNote,
                OtherNote = report.OtherNote,
                CreatedAt = report.CreatedAt ?? DateTime.UtcNow,
                UpdatedAt = report.UpdatedAt ?? DateTime.UtcNow
            };
            
            // Get creator and updater names
            if (report.Creator > 0)
            {
                var creator = await _context.Users.FirstOrDefaultAsync(u => u.Id == report.Creator && !u.Deleted);
                dto.CreatorName = creator?.FullName;
            }
            
            if (report.Updater > 0)
            {
                var updater = await _context.Users.FirstOrDefaultAsync(u => u.Id == report.Updater && !u.Deleted);
                dto.UpdaterName = updater?.FullName;
            }
            
            // Deserialize attachments
            if (report.Attachment != null)
            {
                try
                {
                    dto.Attachment = JsonSerializer.Deserialize<List<AttachmentInfo>>(report.Attachment.RootElement.ToString());
                }
                catch (Exception ex)
                {
                    // Log error and set empty list if deserialization fails
                    Console.WriteLine($"Error deserializing attachments for inspection report {report.Id}: {ex.Message}");
                    dto.Attachment = new List<AttachmentInfo>();
                }
            }
            else
            {
                dto.Attachment = new List<AttachmentInfo>();
            }
            
            return dto;
        }

        private async Task InvalidateInspectionReportCaches(int reportId, int projectId)
        {
            try
            {
                // Invalidate specific caches
                await _cacheService.DeleteAsync(string.Format(RedisCacheKey.INSPECTION_REPORT_BY_ID_CACHE_KEY, reportId));
                await _cacheService.DeleteAsync(string.Format(RedisCacheKey.INSPECTION_REPORT_BY_PROJECT_CACHE_KEY, projectId));
                
                // Invalidate list cache patterns
                await _cacheService.DeleteByPatternAsync(RedisCacheKey.INSPECTION_REPORT_LIST_CACHE_KEY + "*");
                
                // Invalidate all inspection report related caches
                await _cacheService.DeleteByPatternAsync(RedisCacheKey.INSPECTION_REPORT_ALL_PATTERN);
                
                // Also invalidate the main inspection report cache
                await _cacheService.DeleteAsync(RedisCacheKey.INSPECTION_REPORT_CACHE_KEY);
            }
            catch (Exception ex)
            {
                // Log error but don't fail the operation
                Console.WriteLine($"Error invalidating inspection report caches: {ex.Message}");
            }
        }

        /// <summary>
        /// Invalidates construction progress related caches
        /// </summary>
        private async Task InvalidateProgressCache(int progressId, int projectId, int planId)
        {
            // Clear specific caches
            await _cacheService.DeleteAsync(string.Format(RedisCacheKey.CONSTRUCTION_PROGRESS_BY_ID_CACHE_KEY, progressId));
            await _cacheService.DeleteAsync(string.Format(RedisCacheKey.CONSTRUCTION_PROGRESS_BY_PLAN_CACHE_KEY, planId));
            await _cacheService.DeleteAsync(string.Format(RedisCacheKey.CONSTRUCTION_PROGRESS_BY_PROJECT_CACHE_KEY, projectId));
            
            // Clear general cache
            await _cacheService.DeleteAsync(RedisCacheKey.CONSTRUCTION_PROGRESS_CACHE_KEY);
            
            // Clear pattern-based caches
            await _cacheService.DeleteByPatternAsync(RedisCacheKey.CONSTRUCTION_PROGRESS_ALL_PATTERN);
        }

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
            }
            catch (Exception ex)
            {
                // Log error but don't fail the operation
                Console.WriteLine($"Error invalidating project caches: {ex.Message}");
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
            _logger.LogInformation($"Updating parent items progress for {updatedIndices.Count} potentially affected indices");
            
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
                            // 1. Check if ALL children are Done and at 100% progress
                            bool allChildrenDoneAndCompleted = childItems.All(c => 
                                c.Status == ProgressStatusEnum.Done && c.Progress == 100);
                            
                            if (allChildrenDoneAndCompleted)
                            {
                                // If all children are Done and at 100%, set parent to 100% and Done status
                                parentItem.Progress = 100;
                                parentItem.Status = ProgressStatusEnum.Done;
                                _logger.LogInformation($"Setting parent item {parentItem.Id} progress to 100% and status to Done because all children are Done and at 100% progress");
                            }
                            else
                            {
                                // Calculate average progress as before for other cases
                                int totalProgress = 0;
                                foreach (var child in childItems)
                                {
                                    totalProgress += child.Progress;
                                }
                                
                                int averageProgress = totalProgress / childItems.Count;
                                
                                _logger.LogInformation($"Setting progress of parent item {parentItem.Id} (index {index}) to {averageProgress}% based on {childItems.Count} children");
                                
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
                                    _logger.LogInformation($"Setting parent item {parentItem.Id} status to Done because all children are Done");
                                }
                                else if (anyChildInProgress)
                                {
                                    // If any child is InProgress, set parent to InProgress
                                    parentItem.Status = ProgressStatusEnum.InProgress;
                                    _logger.LogInformation($"Setting parent item {parentItem.Id} status to InProgress because at least one child is InProgress");
                                }
                                else if (allChildrenWaitingOrDone)
                                {
                                    // If all children are either WaitForInspection or Done, set parent to WaitForInspection
                                    parentItem.Status = ProgressStatusEnum.WaitForInspection;
                                    _logger.LogInformation($"Setting parent item {parentItem.Id} status to WaitForInspection because all children are WaitForInspection or Done");
                                }
                                else if (allChildrenNotStarted)
                                {
                                    // If all children are NotStarted, set parent to NotStarted
                                    parentItem.Status = ProgressStatusEnum.NotStarted;
                                    _logger.LogInformation($"Setting parent item {parentItem.Id} status to NotStarted because all children are NotStarted");
                                }
                                else 
                                {
                                    // Default fallback based on progress value
                                    if (averageProgress > 0 && averageProgress < 100)
                                    {
                                        parentItem.Status = ProgressStatusEnum.InProgress;
                                        _logger.LogInformation($"Setting parent item {parentItem.Id} status to InProgress based on progress value");
                                    }
                                    else if (averageProgress == 100)
                                    {
                                        parentItem.Status = ProgressStatusEnum.WaitForInspection;
                                        _logger.LogInformation($"Setting parent item {parentItem.Id} status to WaitForInspection based on progress value");
                                    }
                                    else
                                    {
                                        parentItem.Status = ProgressStatusEnum.NotStarted;
                                        _logger.LogInformation($"Setting parent item {parentItem.Id} status to NotStarted based on progress value");
                                    }
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
        /// Rolls back materials from a completed construction progress item to project inventory
        /// based on CanRollBack flag and unused quantities
        /// </summary>
        /// <param name="progressItem">The completed progress item with its details</param>
        /// <param name="actionBy">ID of the user performing the action</param>
        private async Task RollbackMaterials(ConstructionProgressItem progressItem, int actionBy)
        {
            _logger.LogInformation($"Rolling back materials from completed progress item {progressItem.Id} to resource inventory");
            
            // Get progress item details with material resources if they are not already loaded
            var progressItemDetails = progressItem.Details?.Where(d => d.ResourceType == ResourceType.MATERIAL 
                                                                  && d.ResourceId.HasValue 
                                                                  && !d.Deleted).ToList()
                ?? await _context.ConstructionProgressItemDetails
                    .Where(pid => pid.ProgressItemId == progressItem.Id && 
                                pid.ResourceType == ResourceType.MATERIAL && 
                                pid.ResourceId.HasValue &&
                                !pid.Deleted)
                    .ToListAsync();

            // Get vehicle resources associated with this progress item to set their status to Available
            var vehicleDetails = progressItem.Details?.Where(d => d.ResourceType == ResourceType.MACHINE 
                                                              && d.ResourceId.HasValue 
                                                              && !d.Deleted).ToList()
                ?? await _context.ConstructionProgressItemDetails
                    .Where(pid => pid.ProgressItemId == progressItem.Id && 
                                pid.ResourceType == ResourceType.MACHINE && 
                                pid.ResourceId.HasValue &&
                                !pid.Deleted)
                    .ToListAsync();

            if (vehicleDetails != null && vehicleDetails.Any())
            {
                foreach (var vehicleDetail in vehicleDetails)
                {
                    if (vehicleDetail.ResourceId.HasValue)
                    {
                        var vehicle = await _context.Vehicles
                            .FirstOrDefaultAsync(v => v.Id == vehicleDetail.ResourceId.Value && !v.Deleted);
                        
                        if (vehicle != null)
                        {
                            // Set vehicle status to Available
                            vehicle.Status = VehicleStatus.Available;
                            vehicle.UpdatedAt = DateTime.UtcNow;
                            vehicle.Updater = actionBy;
                            
                            _context.Vehicles.Update(vehicle);
                            
                            _logger.LogInformation($"Set vehicle {vehicle.Id} ({vehicle.VehicleName}) status to Available when progress item {progressItem.Id} was approved");
                        }
                    }
                }
            }

            if (!progressItemDetails.Any())
            {
                _logger.LogInformation($"No material details found for progress item {progressItem.Id}");
                return;
            }
            
            int projectId = progressItem.ConstructionProgress.ProjectId;
            
            foreach (var detail in progressItemDetails)
            {
                // Get the material to check rollback flag
                var material = await _context.Materials
                    .FirstOrDefaultAsync(m => m.Id == detail.ResourceId.Value && !m.Deleted);
                
                if (material == null)
                {
                    _logger.LogWarning($"Material with ID {detail.ResourceId.Value} not found or deleted, skipping rollback");
                    continue;
                }
                
                // Calculate quantity to roll back (remaining quantity that wasn't used)
                int unusedQuantity = detail.Quantity - detail.UsedQuantity;
                
                if (unusedQuantity <= 0)
                {
                    _logger.LogInformation($"No unused quantity to roll back for material {material.Id} in progress item detail {detail.Id}");
                    continue;
                }

                // Rollback logic based on CanRollBack flag
                if (material.CanRollBack)
                {
                    // Materials with CanRollBack=true should be returned to ResourceInventory
                    _logger.LogInformation($"Rolling back material {material.Id} ({material.MaterialName}) with CanRollBack=true");
                    await RollbackMaterialToInventory(material, projectId, unusedQuantity, actionBy);
                }
                else
                {
                    // Materials with CanRollBack=false that are leftover (unused) should also be returned to ResourceInventory
                    _logger.LogInformation($"Returning leftover material {material.Id} ({material.MaterialName}) with CanRollBack=false to inventory");
                    await RollbackMaterialToInventory(material, projectId, unusedQuantity, actionBy);
                }
            }
            
            // Note: Cache invalidation for vehicle and resource inventory is now handled
            // at a higher level in the InvalidateAllRelatedCaches method
            
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Adds material quantity back to ResourceInventory
        /// </summary>
        private async Task RollbackMaterialToInventory(Material material, int projectId, int quantity, int actionBy)
        {
            // Check if this material already exists in the project's inventory
            var existingInventory = await _context.ResourceInventory
                .FirstOrDefaultAsync(ri => ri.ProjectId == projectId && 
                                          ri.ResourceId == material.Id && 
                                          ri.ResourceType == ResourceType.MATERIAL && 
                                          !ri.Deleted);
            
            if (existingInventory != null)
            {
                // Update existing inventory record
                existingInventory.Quantity += quantity;
                existingInventory.UpdatedAt = DateTime.Now;
                existingInventory.Updater = actionBy;
                
                _context.ResourceInventory.Update(existingInventory);
                
                _logger.LogInformation($"Added {quantity} of material {material.Id} ({material.MaterialName}) " +
                                     $"to existing project inventory (new total: {existingInventory.Quantity})");
            }
            else
            {
                // Create new inventory record
                var newInventory = new ResourceInventory
                {
                    Name = material.MaterialName,
                    ResourceId = material.Id,
                    ProjectId = projectId,
                    ResourceType = ResourceType.MATERIAL,
                    Quantity = quantity,
                    Unit = material.Unit ?? "unit",
                    Status = true,
                    Creator = actionBy,
                    Updater = actionBy,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                
                await _context.ResourceInventory.AddAsync(newInventory);
                
                _logger.LogInformation($"Created new project inventory record with {quantity} of material {material.Id} ({material.MaterialName})");
            }
        }

        /// <summary>
        /// Sets the project status to WaitingApproveCompleted if all progress items are marked as Done
        /// </summary>
        private async Task SetProjectStatusToWaitingApproveCompleted(int relatedProjectId, int actionBy)
        {
            if (relatedProjectId == 0)
            {
                _logger.LogWarning("Cannot update project status for project ID 0");
                return;
            }

            try
            {
                // Get all progress items for this project
                var allProgressItems = await _context.ConstructionProgresses
                    .Where(cp => cp.ProjectId == relatedProjectId && !cp.Deleted)
                    .SelectMany(cp => cp.ProgressItems.Where(pi => !pi.Deleted))
                    .ToListAsync();

                if (!allProgressItems.Any())
                {
                    _logger.LogWarning("No progress items found for project {ProjectId}", relatedProjectId);
                    return;
                }

                // Check if all items are marked as Done
                bool allCompleted = allProgressItems.All(pi => pi.Status == ProgressStatusEnum.Done);

                if (allCompleted)
                {
                    var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == relatedProjectId && !p.Deleted);
                    
                    // If project exists and is not already in a completed state
                    if (project != null && 
                        project.Status != ProjectStatusEnum.WaitingApproveCompleted && 
                        project.Status != ProjectStatusEnum.Completed && 
                        project.Status != ProjectStatusEnum.Closed)
                    {
                        // Store old status for comparison
                        var oldStatus = project.Status;
                        
                        // Set to waiting for approval
                        project.Status = ProjectStatusEnum.WaitingApproveCompleted;
                        project.UpdatedAt = DateTime.Now;
                        project.Updater = actionBy;
                        
                        _context.Projects.Update(project);
                        
                        // Log the project status change
                        _logger.LogInformation($"All progress items for project {relatedProjectId} are marked as Done, changing status to WaitingApproveCompleted");
                        
                        // Invalidate project caches after status update - use more comprehensive invalidation
                        await InvalidateProjectCaches(relatedProjectId);
                        await _cacheService.DeleteByPatternAsync($"{RedisCacheKey.PROJECT_ALL_PATTERN}*");
                        await _cacheService.DeleteAsync(RedisCacheKey.PROJECT_STATUS_CACHE_KEY);
                        
                        // Also invalidate dashboard and statistics caches since project status is shown there
                        await _cacheService.DeleteByPatternAsync($"DASHBOARD:*");
                        await _cacheService.DeleteByPatternAsync($"STATISTICS:*");
                        await _cacheService.DeleteAsync(string.Format(RedisCacheKey.PROJECT_STATISTICS_PROJECT_CACHE_KEY, relatedProjectId));
                        
                        // Send email notification for the status change
                        if (oldStatus != ProjectStatusEnum.WaitingApproveCompleted)
                        {
                            try
                            {
                                var emailService = _serviceProvider.GetService<IProjectEmailService>();
                                if (emailService != null)
                                {
                                    // Queue a background task to send emails
                                    _ = Task.Run(async () =>
                                    {
                                        await emailService.SendProjectStatusChangeNotification(relatedProjectId, ProjectStatusEnum.WaitingApproveCompleted, actionBy);
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
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating project status for project {ProjectId}: {Message}", relatedProjectId, ex.Message);
            }
        }
    }
} 