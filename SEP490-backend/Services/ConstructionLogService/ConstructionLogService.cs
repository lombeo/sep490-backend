using Microsoft.EntityFrameworkCore;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.ConstructionLog;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Services.CacheService;
using Sep490_Backend.Services.HelperService;
using Sep490_Backend.Services.GoogleDriveService;
using System.Text.Json;
using System.Linq;
using Sep490_Backend.Controllers;

namespace Sep490_Backend.Services.ConstructionLogService
{
    public interface IConstructionLogService
    {
        Task<ConstructionLogDTO> Save(SaveConstructionLogDTO model, int actionBy);
        Task<int> Delete(int id, int actionBy);
        Task<List<ConstructionLogDTO>> List(SearchConstructionLogDTO model);
        Task<ConstructionLogDTO> Detail(int id, int actionBy);
        Task<List<ConstructionLogDTO>> GetByProject(int projectId, int actionBy);
        Task<ResourceLogByTaskDTO> GetResourceLogByTask(int projectId, int taskIndex, int actionBy);
    }

    public class ConstructionLogService : IConstructionLogService
    {
        private readonly BackendContext _context;
        private readonly ICacheService _cacheService;
        private readonly IHelperService _helperService;
        private readonly IGoogleDriveService _googleDriveService;

        public ConstructionLogService(
            BackendContext context,
            ICacheService cacheService,
            IHelperService helperService,
            IGoogleDriveService googleDriveService)
        {
            _context = context;
            _cacheService = cacheService;
            _helperService = helperService;
            _googleDriveService = googleDriveService;
        }

        public async Task<ConstructionLogDTO> Save(SaveConstructionLogDTO model, int actionBy)
        {
            // Check if user is a Construction Manager
            if (!_helperService.IsInRole(actionBy, RoleConstValue.CONSTRUCTION_MANAGER))
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

            var errors = new List<ResponseError>();

            // Validate log code uniqueness if it's a new log or the code has changed
            var existingLogs = await _context.ConstructionLogs
                .Where(cl => !cl.Deleted && cl.LogCode == model.LogCode)
                .ToListAsync();

            if (model.Id == 0) // Creating new log
            {
                if (existingLogs.Any())
                {
                    errors.Add(new ResponseError
                    {
                        Message = Message.ConstructionLogMessage.LOG_CODE_EXISTS,
                        Field = nameof(model.LogCode)
                    });
                }
            }
            else // Updating existing log
            {
                var existingLog = await _context.ConstructionLogs.FirstOrDefaultAsync(cl => cl.Id == model.Id && !cl.Deleted);
                
                if (existingLog == null)
                {
                    throw new KeyNotFoundException(Message.ConstructionLogMessage.NOT_FOUND);
                }
                
                if (existingLog.LogCode != model.LogCode && existingLogs.Any())
                {
                    errors.Add(new ResponseError
                    {
                        Message = Message.ConstructionLogMessage.LOG_CODE_EXISTS,
                        Field = nameof(model.LogCode)
                    });
                }
            }

            if (errors.Count > 0)
            {
                throw new ValidationException(errors);
            }

            // Handle file attachments
            List<AttachmentDTO> attachmentInfos = new List<AttachmentDTO>();
            
            // Get existing attachments if updating
            if (model.Id != 0)
            {
                var existingLog = await _context.ConstructionLogs.FirstOrDefaultAsync(cl => cl.Id == model.Id && !cl.Deleted);
                if (existingLog?.Attachments != null)
                {
                    attachmentInfos = JsonSerializer.Deserialize<List<AttachmentDTO>>(existingLog.Attachments.RootElement.ToString());
                    
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
                        
                        attachmentInfos.Add(new AttachmentDTO
                        {
                            Id = fileId,
                            Name = file.FileName,
                            WebViewLink = $"https://drive.google.com/file/d/{fileId}/view",
                            WebContentLink = uploadResult
                        });
                    }
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
                constructionLog.LogCode = model.LogCode;
                constructionLog.LogName = model.LogName;
                constructionLog.LogDate = model.LogDate;
                constructionLog.Resources = model.Resources != null 
                    ? JsonDocument.Parse(JsonSerializer.Serialize(model.Resources))
                    : null;
                constructionLog.WorkAmount = model.WorkAmount != null 
                    ? JsonDocument.Parse(JsonSerializer.Serialize(model.WorkAmount))
                    : null;
                constructionLog.Weather = model.Weather != null 
                    ? JsonDocument.Parse(JsonSerializer.Serialize(model.Weather))
                    : null;
                constructionLog.Safety = model.Safety;
                constructionLog.Quality = model.Quality;
                constructionLog.Progress = model.Progress;
                constructionLog.Problem = model.Problem;
                constructionLog.Advice = model.Advice;
                constructionLog.Images = model.Images != null && model.Images.Any() 
                    ? JsonDocument.Parse(JsonSerializer.Serialize(model.Images))
                    : null;
                constructionLog.Attachments = attachmentInfos.Any() 
                    ? JsonDocument.Parse(JsonSerializer.Serialize(attachmentInfos))
                    : null;
                constructionLog.Note = model.Note;
                constructionLog.UpdatedAt = DateTime.UtcNow;
                constructionLog.Updater = actionBy;
                
                _context.Update(constructionLog);
            }
            else // Create new log
            {
                constructionLog = new ConstructionLog
                {
                    ProjectId = model.ProjectId,
                    LogCode = model.LogCode,
                    LogName = model.LogName,
                    LogDate = model.LogDate,
                    Resources = model.Resources != null 
                        ? JsonDocument.Parse(JsonSerializer.Serialize(model.Resources))
                        : null,
                    WorkAmount = model.WorkAmount != null 
                        ? JsonDocument.Parse(JsonSerializer.Serialize(model.WorkAmount))
                        : null,
                    Weather = model.Weather != null 
                        ? JsonDocument.Parse(JsonSerializer.Serialize(model.Weather))
                        : null,
                    Safety = model.Safety,
                    Quality = model.Quality,
                    Progress = model.Progress,
                    Problem = model.Problem,
                    Advice = model.Advice,
                    Images = model.Images != null && model.Images.Any() 
                        ? JsonDocument.Parse(JsonSerializer.Serialize(model.Images))
                        : null,
                    Attachments = attachmentInfos.Any() 
                        ? JsonDocument.Parse(JsonSerializer.Serialize(attachmentInfos))
                        : null,
                    Note = model.Note,
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
            string cacheKey = $"{RedisCacheKey.CONSTRUCTION_LOG_LIST_CACHE_KEY}:{model.ProjectId}:{model.FromDate}:{model.ToDate}:{model.LogCode}:{model.LogName}:{model.TaskIndex}:{model.Page}:{model.PageSize}";
            
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
            
            if (model.TaskIndex.HasValue)
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
                        var workAmounts = JsonSerializer.Deserialize<List<WorkAmountDTO>>(log.WorkAmount.RootElement.ToString());
                        if (workAmounts != null && workAmounts.Any(wa => wa.TaskIndex == model.TaskIndex))
                        {
                            hasMatchingTask = true;
                        }
                    }
                    
                    // Check resources for task index
                    if (!hasMatchingTask && log.Resources != null)
                    {
                        var resources = JsonSerializer.Deserialize<List<ConstructionLogResourceDTO>>(log.Resources.RootElement.ToString());
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

        public async Task<ResourceLogByTaskDTO> GetResourceLogByTask(int projectId, int taskIndex, int actionBy)
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
                .Where(cl => cl.ProjectId == projectId && !cl.Deleted)
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
                    var workAmounts = JsonSerializer.Deserialize<List<WorkAmountDTO>>(log.WorkAmount.RootElement.ToString());
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
                    var resources = JsonSerializer.Deserialize<List<ConstructionLogResourceDTO>>(log.Resources.RootElement.ToString());
                    var matchingResources = resources?.Where(r => r.TaskIndex == taskIndex);
                    
                    if (matchingResources != null && matchingResources.Any())
                    {
                        foreach (var resource in matchingResources)
                        {
                            // Calculate hours from start/end time
                            var startTime = TimeSpan.Parse(resource.StartTime);
                            var endTime = TimeSpan.Parse(resource.EndTime);
                            var hours = (endTime - startTime).TotalHours;
                            
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
                    ? JsonSerializer.Deserialize<List<ConstructionLogResourceDTO>>(log.Resources.RootElement.ToString())
                    : new List<ConstructionLogResourceDTO>(),
                WorkAmount = log.WorkAmount != null 
                    ? JsonSerializer.Deserialize<List<WorkAmountDTO>>(log.WorkAmount.RootElement.ToString())
                    : new List<WorkAmountDTO>(),
                Weather = log.Weather != null 
                    ? JsonSerializer.Deserialize<List<WeatherDTO>>(log.Weather.RootElement.ToString())
                    : new List<WeatherDTO>(),
                Safety = log.Safety,
                Quality = log.Quality,
                Progress = log.Progress,
                Problem = log.Problem,
                Advice = log.Advice,
                Images = log.Images != null 
                    ? JsonSerializer.Deserialize<List<string>>(log.Images.RootElement.ToString())
                    : new List<string>(),
                Attachments = log.Attachments != null 
                    ? JsonSerializer.Deserialize<List<AttachmentDTO>>(log.Attachments.RootElement.ToString())
                    : new List<AttachmentDTO>(),
                Note = log.Note,
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
    }
} 