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

namespace Sep490_Backend.Services.InspectionReportService
{
    public class InspectionReportService : IInspectionReportService
    {
        private readonly BackendContext _context;
        private readonly ICacheService _cacheService;
        private readonly IHelperService _helperService;
        private readonly IGoogleDriveService _googleDriveService;
        private readonly ILogger<InspectionReportService> _logger;
        private readonly IConstructionProgressService _constructionProgressService;

        public InspectionReportService(
            BackendContext context,
            ICacheService cacheService,
            IHelperService helperService,
            IGoogleDriveService googleDriveService,
            ILogger<InspectionReportService> logger,
            IConstructionProgressService constructionProgressService)
        {
            _context = context;
            _cacheService = cacheService;
            _helperService = helperService;
            _googleDriveService = googleDriveService;
            _logger = logger;
            _constructionProgressService = constructionProgressService;
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
                return await UpdateInspectionReportStatus(model.Id, model.Status.Value, actionBy);
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

            // Handle file attachments
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
                            Console.WriteLine($"Failed to delete old attachments: {ex.Message}");
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
                            WebContentLink = uploadResult,
                            MimeType = file.ContentType
                        });
                    }
                }
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
                    Attachment = JsonSerializer.SerializeToDocument(attachmentInfos),
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
                    
                    // Update parent progress calculation if needed
                    if (becameCompleted && !string.IsNullOrEmpty(progressItem.ParentIndex))
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
                        
                        // Ensure all parent progress updates are saved to the database
                        await _context.SaveChangesAsync();
                    }
                    
                    // Check if all progress items for the project are now Done, and if so,
                    // update the project status to WaitingApproveCompleted
                    var relatedProjectId = progressItem.ConstructionProgress.ProjectId;
                    var projectProgressItems = await _context.ConstructionProgresses
                        .Where(cp => cp.ProjectId == relatedProjectId && !cp.Deleted)
                        .SelectMany(cp => cp.ProgressItems.Where(pi => !pi.Deleted))
                        .ToListAsync();
                        
                    if (projectProgressItems.Any() && projectProgressItems.All(pi => pi.Status == ProgressStatusEnum.Done))
                    {
                        var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == relatedProjectId && !p.Deleted);
                        
                        if (project != null && 
                            project.Status != ProjectStatusEnum.WaitingApproveCompleted && 
                            project.Status != ProjectStatusEnum.Completed && 
                            project.Status != ProjectStatusEnum.Closed)
                        {
                            project.Status = ProjectStatusEnum.WaitingApproveCompleted;
                            project.UpdatedAt = DateTime.Now;
                            project.Updater = actionBy;
                            
                            _context.Projects.Update(project);
                            
                            // Log the project status change
                            _logger.LogInformation($"All progress items for project {relatedProjectId} are marked as Done, changing status to WaitingApproveCompleted");
                            
                            // Invalidate project caches after status update
                            await InvalidateProjectCaches(relatedProjectId);
                        }
                    }
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
                inspectionReport.Attachment = JsonSerializer.SerializeToDocument(attachmentInfos);
                
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
            await InvalidateProgressCache(progressId, projectId, planId);
            
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
            
            // If status is being set to Approved and the inspection decision is Pass,
            // update the construction progress item status and progress
            if (status == InspectionReportStatus.Approved && 
                (InspectionDecision)report.InspectionDecision == InspectionDecision.Pass)
            {
                var progressItem = await _context.ConstructionProgressItems
                    .Include(pi => pi.ConstructionProgress)
                    .FirstOrDefaultAsync(pi => pi.Id == report.ConstructionProgressItemId && !pi.Deleted);
                
                if (progressItem != null)
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
                    
                    // Update parent progress calculation if needed
                    if (becameCompleted && !string.IsNullOrEmpty(progressItem.ParentIndex))
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
                        
                        // Ensure all parent progress updates are saved to the database
                        await _context.SaveChangesAsync();
                    }
                    
                    // Check if all progress items for the project are now Done, and if so,
                    // update the project status to WaitingApproveCompleted
                    var relatedProjectId = progressItem.ConstructionProgress.ProjectId;
                    var projectProgressItems = await _context.ConstructionProgresses
                        .Where(cp => cp.ProjectId == relatedProjectId && !cp.Deleted)
                        .SelectMany(cp => cp.ProgressItems.Where(pi => !pi.Deleted))
                        .ToListAsync();
                        
                    if (projectProgressItems.Any() && projectProgressItems.All(pi => pi.Status == ProgressStatusEnum.Done))
                    {
                        var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == relatedProjectId && !p.Deleted);
                        
                        if (project != null && 
                            project.Status != ProjectStatusEnum.WaitingApproveCompleted && 
                            project.Status != ProjectStatusEnum.Completed && 
                            project.Status != ProjectStatusEnum.Closed)
                        {
                            project.Status = ProjectStatusEnum.WaitingApproveCompleted;
                            project.UpdatedAt = DateTime.Now;
                            project.Updater = actionBy;
                            
                            _context.Projects.Update(project);
                            
                            // Log the project status change
                            _logger.LogInformation($"All progress items for project {relatedProjectId} are marked as Done, changing status to WaitingApproveCompleted");
                            
                            // Invalidate project caches after status update
                            await InvalidateProjectCaches(relatedProjectId);
                        }
                    }
                }
            }
            
            await _context.SaveChangesAsync();
            
            // Invalidate cache
            int projectId = report.ConstructionProgressItem.ConstructionProgress.ProjectId;
            await InvalidateInspectionReportCaches(report.Id, projectId);
            
            // Invalidate construction progress caches if we updated a progress item
            if (status == InspectionReportStatus.Approved && 
                (InspectionDecision)report.InspectionDecision == InspectionDecision.Pass)
            {
                var progressId = report.ConstructionProgressItem.ConstructionProgress.Id;
                var planId = report.ConstructionProgressItem.ConstructionProgress.PlanId;
                await InvalidateProgressCache(progressId, projectId, planId);
            }
            
            // Return updated report
            return await MapToInspectionReportDTO(report);
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
                                          ir.QualityNote.Contains(model.Keyword) ||
                                          ir.OtherNote.Contains(model.Keyword));
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
    }
} 