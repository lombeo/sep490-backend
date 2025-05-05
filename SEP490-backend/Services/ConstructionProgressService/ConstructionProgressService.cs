using Microsoft.EntityFrameworkCore;
using Sep490_Backend.DTO;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.ConstructionProgress;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Infra.Enums;
using Sep490_Backend.Services.CacheService;
using Sep490_Backend.Services.DataService;
using Sep490_Backend.Services.HelperService;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Sep490_Backend.Services.ProjectService;
using System.Text.Json;

namespace Sep490_Backend.Services.ConstructionProgressService
{
    public interface IConstructionProgressService
    {
        Task<List<ConstructionProgressDTO>> Search(ConstructionProgressQuery query);
        Task<ConstructionProgressDTO> GetById(int id, int actionBy);
        Task<ConstructionProgressDTO> GetByProjectId(int projectId, int actionBy);
        Task<bool> Update(UpdateProgressItemsDTO model, int actionBy);
        Task<ProgressItemDTO> CreateProgressItem(SaveProgressItemDTO model, int actionBy);
        Task<ProgressItemDTO> UpdateProgressItem(UpdateProgressItemDTO model, int actionBy);
    }

    public class ConstructionProgressService : IConstructionProgressService
    {
        private readonly BackendContext _context;
        private readonly ICacheService _cacheService;
        private readonly IHelperService _helperService;
        private readonly IDataService _dataService;
        private readonly TimeSpan _cacheExpirationTime = TimeSpan.FromMinutes(30);
        private readonly ILogger<ConstructionProgressService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public ConstructionProgressService(
            BackendContext context,
            ICacheService cacheService,
            IHelperService helperService,
            IDataService dataService,
            ILogger<ConstructionProgressService> logger,
            IServiceProvider serviceProvider)
        {
            _context = context;
            _cacheService = cacheService;
            _helperService = helperService;
            _dataService = dataService;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task<List<ConstructionProgressDTO>> Search(ConstructionProgressQuery query)
        {
            // Check authorization - user must be part of the project if ProjectId is specified
            if (query.ProjectId.HasValue)
            {
                bool isAuthorized = _helperService.IsInRole(query.ActionBy, RoleConstValue.EXECUTIVE_BOARD) ||
                    await _context.ProjectUsers.AnyAsync(pu => 
                        pu.ProjectId == query.ProjectId.Value && 
                        pu.UserId == (query.ActionBy) && 
                        !pu.Deleted);
                
                if (!isAuthorized)
                {
                    throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED_PROJECT);
                }
            }

            var cacheKey = $"{RedisCacheKey.CONSTRUCTION_PROGRESS_CACHE_KEY}:{(query.ProjectId.HasValue ? $"PROJECT:{query.ProjectId}:" : "")}:{(query.PlanId.HasValue ? $"PLAN:{query.PlanId}:" : "")}:PAGE:{query.PageIndex}:SIZE:{query.PageSize}";

            // Try to get from cache
            var cachedResult = await _cacheService.GetAsync<List<ConstructionProgressDTO>>(cacheKey);
            if (cachedResult != null)
            {
                return cachedResult;
            }

            // Build query
            var progressQuery = _context.ConstructionProgresses
                .Include(cp => cp.ProgressItems).ThenInclude(pi => pi.Details)
                .AsNoTracking()
                .AsQueryable();

            // Apply filters
            if (query.ProjectId.HasValue)
            {
                progressQuery = progressQuery.Where(cp => cp.ProjectId == query.ProjectId.Value);
            }
            else if (!_helperService.IsInRole(query.ActionBy, RoleConstValue.EXECUTIVE_BOARD))
            {
                // If not Executive Board, limit to projects the user is part of
                var userProjects = await _context.ProjectUsers
                    .Where(pu => pu.UserId == (query.ActionBy) && !pu.Deleted)
                    .Select(pu => pu.ProjectId)
                    .ToListAsync();
                
                progressQuery = progressQuery.Where(cp => userProjects.Contains(cp.ProjectId));
            }

            if (query.PlanId.HasValue)
            {
                progressQuery = progressQuery.Where(cp => cp.PlanId == query.PlanId.Value);
            }

            // Get total count
            query.Total = await progressQuery.CountAsync();

            // Apply pagination
            var pagedResult = await progressQuery
                .OrderByDescending(cp => cp.CreatedAt)
                .Skip((query.PageIndex - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();

            // Map to DTOs
            var result = new List<ConstructionProgressDTO>();
            foreach (var progress in pagedResult)
            {
                var dto = await MapToDTO(progress);
                result.Add(dto);
            }

            // Cache the result
            await _cacheService.SetAsync(cacheKey, result, _cacheExpirationTime);

            return result;
        }

        public async Task<ConstructionProgressDTO> GetById(int id, int actionBy)
        {
            // Check authorization
            if (!await IsAuthorizedToViewProgress(id, actionBy))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Try to get from cache
            string cacheKey = string.Format(RedisCacheKey.CONSTRUCTION_PROGRESS_BY_ID_CACHE_KEY, id);
            var cachedResult = await _cacheService.GetAsync<ConstructionProgressDTO>(cacheKey);
            if (cachedResult != null)
            {
                return cachedResult;
            }

            // Get from database
            var progress = await _context.ConstructionProgresses
                .Include(cp => cp.Project)
                .Include(cp => cp.ConstructionPlan)
                .Include(cp => cp.ProgressItems)
                .ThenInclude(pi => pi.Details)
                .FirstOrDefaultAsync(cp => cp.Id == id && !cp.Deleted);

            if (progress == null)
            {
                throw new KeyNotFoundException(Message.ConstructionProgressMessage.NOT_FOUND);
            }

            // Map to DTO
            var result = await MapToDTO(progress);

            // Cache the result
            await _cacheService.SetAsync(cacheKey, result, _cacheExpirationTime);

            return result;
        }

        public async Task<ConstructionProgressDTO> GetByProjectId(int projectId, int actionBy)
        {
            // Check if project exists
            var project = await _context.Projects
                .FirstOrDefaultAsync(p => p.Id == projectId && !p.Deleted);

            if (project == null)
            {
                throw new KeyNotFoundException(Message.ConstructionProgressMessage.INVALID_PROJECT);
            }

            // Check authorization - user must be part of the project
            var isUserInProject = await _context.ProjectUsers
                .AnyAsync(pu => pu.ProjectId == projectId && pu.UserId == actionBy && !pu.Deleted);

            if (!isUserInProject && !_helperService.IsInRole(actionBy, RoleConstValue.EXECUTIVE_BOARD))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED_PROJECT);
            }

            // Try to get from cache
            string cacheKey = string.Format(RedisCacheKey.CONSTRUCTION_PROGRESS_BY_PROJECT_CACHE_KEY, projectId);
            var cachedResult = await _cacheService.GetAsync<ConstructionProgressDTO>(cacheKey);
            if (cachedResult != null)
            {
                return cachedResult;
            }

            // Get from database
            var progress = await _context.ConstructionProgresses
                .Include(cp => cp.Project)
                .Include(cp => cp.ConstructionPlan)
                .Include(cp => cp.ProgressItems)
                .ThenInclude(pi => pi.Details)
                .FirstOrDefaultAsync(cp => cp.ProjectId == projectId && !cp.Deleted);

            if (progress == null)
            {
                throw new KeyNotFoundException(Message.ConstructionProgressMessage.NOT_FOUND);
            }

            // Map to DTO
            var result = await MapToDTO(progress);

            // Cache the result
            await _cacheService.SetAsync(cacheKey, result, _cacheExpirationTime);

            return result;
        }

        public async Task<bool> Update(UpdateProgressItemsDTO model, int actionBy)
        {
            // Begin transaction
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                // Check if progress exists
                var progress = await _context.ConstructionProgresses
                    .Include(cp => cp.Project)
                    .FirstOrDefaultAsync(cp => cp.Id == model.ProgressId && !cp.Deleted);

                if (progress == null)
                {
                    throw new KeyNotFoundException(Message.ConstructionProgressMessage.NOT_FOUND);
                }

                // Check authorization - user must be part of the project
                var isUserInProject = await _context.ProjectUsers
                    .AnyAsync(pu => pu.ProjectId == progress.ProjectId && pu.UserId == actionBy && !pu.Deleted);

                if (!isUserInProject && !_helperService.IsInRole(actionBy, RoleConstValue.EXECUTIVE_BOARD))
                {
                    throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED_PROJECT);
                }

                // Get all progress items at once to avoid multiple queries
                var progressItemIds = model.Items.Select(i => i.Id).ToList();
                var progressItems = await _context.ConstructionProgressItems
                    .Where(pi => progressItemIds.Contains(pi.Id) && pi.ProgressId == model.ProgressId && !pi.Deleted)
                    .ToListAsync();

                if (progressItems.Count == 0)
                {
                    throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
                }

                // Track items that have been newly completed (Progress = 100%)
                var newlyCompletedItems = new List<ConstructionProgressItem>();
                // Track updated items for parent progress calculation
                var updatedItemIndices = new HashSet<string>();

                // Update progress items
                foreach (var updateItem in model.Items)
                {
                    var progressItem = progressItems.FirstOrDefault(pi => pi.Id == updateItem.Id);
                    if (progressItem == null)
                    {
                        continue;
                    }

                    // Validate progress value
                    if (updateItem.Progress < 0 || updateItem.Progress > 100)
                    {
                        throw new ArgumentException(Message.ConstructionProgressMessage.INVALID_PROGRESS);
                    }

                    // Validate status
                    if (!Enum.IsDefined(typeof(ProgressStatusEnum), updateItem.Status))
                    {
                        throw new ArgumentException(Message.ConstructionProgressMessage.INVALID_STATUS);
                    }

                    // Check if item is becoming completed
                    bool becameCompleted = progressItem.Progress < 100 && updateItem.Progress == 100;

                    // Update progress item
                    progressItem.Progress = updateItem.Progress;
                    // Update status based on progress value
                    if (updateItem.Progress > 0 && updateItem.Progress < 100)
                    {
                        progressItem.Status = ProgressStatusEnum.InProgress;
                    }
                    else if (updateItem.Progress == 100)
                    {
                        progressItem.Status = ProgressStatusEnum.WaitForInspection;
                        
                        // If the item just became completed, add it to our tracked list
                        if (becameCompleted)
                        {
                            newlyCompletedItems.Add(progressItem);
                        }
                    }
                    else
                    {
                        progressItem.Status = (ProgressStatusEnum)updateItem.Status;
                    }
                    
                    progressItem.ActualStartDate = updateItem.ActualStartDate;
                    progressItem.ActualEndDate = updateItem.ActualEndDate;
                    progressItem.Updater = actionBy;

                    _context.ConstructionProgressItems.Update(progressItem);
                    
                    // Add to updated items set for parent progress calculation
                    updatedItemIndices.Add(progressItem.Index);
                    
                    // Add parent index to updated items set (if it exists)
                    if (!string.IsNullOrEmpty(progressItem.ParentIndex))
                    {
                        updatedItemIndices.Add(progressItem.ParentIndex);
                    }
                }

                // Process newly completed items for material rollback
                if (newlyCompletedItems.Any())
                {
                    foreach (var completedItem in newlyCompletedItems)
                    {
                        await RollbackMaterialsFromCompletedItem(completedItem.Id, progress.ProjectId, actionBy);
                    }
                }
                
                // Get all progress items for this progress to calculate parent progress
                var allProgressItems = await _context.ConstructionProgressItems
                    .Where(pi => pi.ProgressId == model.ProgressId && !pi.Deleted)
                    .ToListAsync();
                    
                // Update progress of parent items based on their children
                await UpdateParentItemsProgress(allProgressItems, updatedItemIndices, actionBy);
                
                // Ensure all parent progress updates are saved to the database
                await _context.SaveChangesAsync();

                // Update project status based on all progress items
                await UpdateProjectStatusBasedOnProgress(progress.ProjectId, actionBy);

                // Update progress entity
                progress.Updater = actionBy;
                _context.ConstructionProgresses.Update(progress);

                // Save changes
                await _context.SaveChangesAsync();
                
                // Commit transaction
                await transaction.CommitAsync();

                // Use comprehensive cache invalidation for all affected entities
                // Since this is a bulk update, we'll clear all caches related to progress items
                await InvalidateProgressItemRelatedCaches(0, progress.Id, progress.ProjectId, progress.PlanId);

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating construction progress: {message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Rolls back materials from a completed construction progress item to project inventory
        /// </summary>
        /// <param name="progressItemId">ID of the completed progress item</param>
        /// <param name="projectId">ID of the project</param>
        /// <param name="actionBy">ID of the user performing the action</param>
        private async Task RollbackMaterialsFromCompletedItem(int progressItemId, int projectId, int actionBy)
        {
            // Get progress item details containing materials
            var progressItemDetails = await _context.ConstructionProgressItemDetails
                .Where(pid => pid.ProgressItemId == progressItemId && 
                              pid.ResourceType == ResourceType.MATERIAL && 
                              pid.ResourceId.HasValue &&
                             pid.Quantity > pid.UsedQuantity && 
                              !pid.Deleted)
                .ToListAsync();
                
            if (!progressItemDetails.Any())
            {
                return;
            }
            
            foreach (var detail in progressItemDetails)
            {
                // Get the material to check rollback flag
                var material = await _context.Materials
                    .FirstOrDefaultAsync(m => m.Id == detail.ResourceId.Value && !m.Deleted);
                
                if (material == null)
                {
                    continue;
                }
                
                // Calculate quantity to roll back (remaining quantity that wasn't used)
                int rollbackQuantity = detail.Quantity - detail.UsedQuantity;
                
                // Check if material can be rolled back
                if (material.CanRollBack)
                {
                    // Check if this material already exists in inventory
                var existingInventory = await _context.ResourceInventory
                    .FirstOrDefaultAsync(ri => ri.ProjectId == projectId && 
                                                 ri.ResourceId == material.Id &&
                                              ri.ResourceType == ResourceType.MATERIAL && 
                                              !ri.Deleted);
                if (existingInventory != null)
                {
                        // Update existing inventory
                        existingInventory.Quantity += rollbackQuantity;
                    existingInventory.Updater = actionBy;
                        existingInventory.UpdatedAt = DateTime.Now;
                    
                    _context.ResourceInventory.Update(existingInventory);
                }
                else
                {
                        // Create new inventory entry
                    var newInventory = new ResourceInventory
                    {
                        ProjectId = projectId,
                            ResourceId = material.Id,
                        ResourceType = ResourceType.MATERIAL,
                            Name = material.MaterialName,
                            Unit = material.Unit,
                            Quantity = rollbackQuantity,
                        Status = true,
                        Creator = actionBy,
                        Updater = actionBy,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    
                    await _context.ResourceInventory.AddAsync(newInventory);
                    }
                    
                    // Update progress item detail used quantity
                    detail.UsedQuantity = detail.Quantity;
                }
                
                _context.ConstructionProgressItemDetails.Update(detail);
            }
            
            // Note: Cache invalidation is now handled by the comprehensive 
            // InvalidateProgressItemRelatedCaches method, so we don't need to invalidate caches here.
            
            // Save is handled by the calling method in its transaction
        }

        /// <summary>
        /// Updates the project status based on the completion of all progress items
        /// </summary>
        /// <param name="projectId">ID of the project</param>
        /// <param name="actionBy">ID of the user performing the action</param>
        private async Task UpdateProjectStatusBasedOnProgress(int projectId, int actionBy)
        {
            // Get the project
            var project = await _context.Projects
                .FirstOrDefaultAsync(p => p.Id == projectId && !p.Deleted);
                
            if (project == null)
            {
                _logger.LogWarning($"Project with ID {projectId} not found or deleted");
                return;
            }
            
            // Get all progress items for this project across all construction progresses
            var allProgressItems = await _context.ConstructionProgresses
                .Where(cp => cp.ProjectId == projectId && !cp.Deleted)
                .SelectMany(cp => cp.ProgressItems.Where(pi => !pi.Deleted))
                .ToListAsync();
                
            if (!allProgressItems.Any())
            {
                _logger.LogInformation($"No progress items found for project {projectId}");
                return;
            }
            
            // Determine if all progress items are completed (status = Done)
            bool allCompleted = allProgressItems.All(pi => pi.Status == ProgressStatusEnum.Done);
            bool anyIncomplete = allProgressItems.Any(pi => pi.Status != ProgressStatusEnum.Done);
            
            // Track if status will change for sending email
            bool statusChanged = false;
            ProjectStatusEnum newStatus = project.Status;
            
            // If the project is in WaitingApproveCompleted status but we have incomplete items,
            // change back to InProgress
            if (project.Status == ProjectStatusEnum.WaitingApproveCompleted && anyIncomplete)
            {
                _logger.LogInformation($"Project {projectId} has incomplete items, changing status from WaitingApproveCompleted to InProgress");
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
                _logger.LogInformation($"All progress items for project {projectId} are marked as Done, changing status to WaitingApproveCompleted");
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

        private async Task<bool> IsAuthorizedToViewProgress(int progressId, int userId)
        {
            // Get project ID for the progress
            var projectId = await _context.ConstructionProgresses
                .Where(cp => cp.Id == progressId && !cp.Deleted)
                .Select(cp => cp.ProjectId)
                .FirstOrDefaultAsync();

            if (projectId == 0)
            {
                return false;
            }

            // Executive Board can access all projects
            if (_helperService.IsInRole(userId, RoleConstValue.EXECUTIVE_BOARD))
            {
                return true;
            }

            // Check if user is part of the project
            return await _context.ProjectUsers
                .AnyAsync(pu => pu.ProjectId == projectId && pu.UserId == userId && !pu.Deleted);
        }

        private async Task<ConstructionProgressDTO> MapToDTO(ConstructionProgress progress)
        {
            var dto = new ConstructionProgressDTO
            {
                Id = progress.Id,
                ProjectId = progress.ProjectId,
                PlanId = progress.PlanId,
                CreatedAt = progress.CreatedAt,
                UpdatedAt = progress.UpdatedAt,
                CreatedBy = progress.Creator,
                UpdatedBy = progress.Updater
            };

            // Add creator and updater names
            if (progress.Creator > 0)
            {
                var creator = await _context.Users.FirstOrDefaultAsync(u => u.Id == progress.Creator);
                dto.CreatedByName = creator?.FullName ?? string.Empty;
            }

            if (progress.Updater > 0)
            {
                var updater = await _context.Users.FirstOrDefaultAsync(u => u.Id == progress.Updater);
                dto.UpdatedByName = updater?.FullName ?? string.Empty;
            }

            // Add progress items
            foreach (var item in progress.ProgressItems.Where(pi => !pi.Deleted))
            {
                var itemDto = new ProgressItemDTO
                {
                    Id = item.Id,
                    WorkCode = item.WorkCode,
                    Index = item.Index,
                    ParentIndex = item.ParentIndex,
                    WorkName = item.WorkName,
                    Unit = item.Unit,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    TotalPrice = item.TotalPrice,
                    Progress = item.Progress,
                    Status = (int)item.Status,
                    PlanStartDate = item.PlanStartDate,
                    PlanEndDate = item.PlanEndDate,
                    ActualStartDate = item.ActualStartDate,
                    ActualEndDate = item.ActualEndDate,
                    UsedQuantity = item.UsedQuantity,
                    ItemRelations = item.ItemRelations
                };

                // Add item details
                foreach (var detail in item.Details.Where(d => !d.Deleted))
                {
                    var detailDto = new ProgressItemDetailDTO
                    {
                        Id = detail.Id,
                        ProgressItemId = detail.ProgressItemId,
                        WorkCode = detail.WorkCode,
                        ResourceType = (int)detail.ResourceType,
                        Quantity = detail.Quantity,
                        UsedQuantity = detail.UsedQuantity,
                        Unit = detail.Unit ?? string.Empty,
                        UnitPrice = detail.UnitPrice,
                        Total = detail.Total,
                        ResourceId = detail.ResourceId
                    };

                    if (detail.ResourceId.HasValue)
                    {
                        detailDto.Resource = await GetResourceDTO(detail.ResourceType, detail.ResourceId.Value);
                    }

                    itemDto.Details.Add(detailDto);
                }

                dto.ProgressItems.Add(itemDto);
            }

            // Process item relations for predecessor format
            dto.ProgressItems = ConvertProgressToTasks(dto.ProgressItems);

            return dto;
        }

        private List<ProgressItemDTO> ConvertProgressToTasks(List<ProgressItemDTO> progressItems)
        {
            foreach (var item in progressItems)
            {
                if (item.ItemRelations != null && item.ItemRelations.Any())
                {
                    foreach (var relation in item.ItemRelations)
                    {
                        var relatedTask = progressItems.FirstOrDefault(t => t.Index == relation.Key);
                        if (relatedTask != null)
                        {
                            var newRelation = $"{item.Index.Trim()}{relation.Value.Trim()}";
                            if (string.IsNullOrEmpty(relatedTask.Predecessor))
                            {
                                relatedTask.Predecessor = newRelation;
                            }
                            else
                            {
                                relatedTask.Predecessor += $",{newRelation}";
                            }
                        }
                    }
                }
            }

            return progressItems;
        }

        private async Task<ResourceDTO> GetResourceDTO(ResourceType resourceType, int resourceId)
        {
            var resource = new ResourceDTO
            {
                Id = resourceId,
                Type = (int)resourceType
            };

            switch (resourceType)
            {
                case ResourceType.HUMAN:
                    var team = await _context.ConstructionTeams.FirstOrDefaultAsync(t => t.Id == resourceId && !t.Deleted);
                    if (team != null)
                    {
                        resource.Name = team.TeamName;
                    }
                    break;

                case ResourceType.MACHINE:
                    var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.Id == resourceId && !v.Deleted);
                    if (vehicle != null)
                    {
                        resource.Name = vehicle.LicensePlate;
                    }
                    break;

                case ResourceType.MATERIAL:
                    var material = await _context.Materials.FirstOrDefaultAsync(m => m.Id == resourceId && !m.Deleted);
                    if (material != null)
                    {
                        resource.Name = material.MaterialName;
                    }
                    break;
            }

            return resource;
        }

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

        public async Task<ProgressItemDTO> CreateProgressItem(SaveProgressItemDTO model, int actionBy)
        {
            // Begin transaction
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                // Check if progress exists
                var progress = await _context.ConstructionProgresses
                    .Include(cp => cp.Project)
                    .FirstOrDefaultAsync(cp => cp.Id == model.ProgressId && !cp.Deleted);

                if (progress == null)
                {
                    throw new KeyNotFoundException(Message.ConstructionProgressMessage.NOT_FOUND);
                }

                // Check authorization - user must be a Technical Manager in the project
                var isUserTechnicalManager = await _context.ProjectUsers
                    .Include(pu => pu.User)
                    .AnyAsync(pu => 
                        pu.ProjectId == progress.ProjectId && 
                        pu.UserId == actionBy && 
                        pu.User.Role == RoleConstValue.TECHNICAL_MANAGER &&
                        !pu.Deleted);

                if (!isUserTechnicalManager && !_helperService.IsInRole(actionBy, RoleConstValue.EXECUTIVE_BOARD))
                {
                    throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED_PROJECT);
                }

                // Validate progress value
                if (model.Progress < 0 || model.Progress > 100)
                {
                    throw new ArgumentException(Message.ConstructionProgressMessage.INVALID_PROGRESS);
                }

                // Validate status
                if (!Enum.IsDefined(typeof(ProgressStatusEnum), model.Status))
                {
                    throw new ArgumentException(Message.ConstructionProgressMessage.INVALID_STATUS);
                }

                // Generate a unique WorkCode
                string projectPrefix = $"EW{progress.ProjectId:D2}";
                int mainItemCounter = 1;
                var subItemCounters = new Dictionary<string, int>();
                
                // Get existing work codes to ensure uniqueness
                var existingWorkCodes = await _context.ConstructionProgressItems
                    .Where(pi => pi.ProgressId == model.ProgressId && !pi.Deleted)
                    .Select(pi => pi.WorkCode)
                    .ToListAsync();
                
                string workCode = await GenerateUniqueWorkCode(projectPrefix, model.Index, mainItemCounter, subItemCounters, existingWorkCodes);

                // Create new progress item
                var progressItem = new ConstructionProgressItem
                {
                    ProgressId = model.ProgressId,
                    WorkCode = workCode,
                    Index = model.Index,
                    ParentIndex = model.ParentIndex,
                    WorkName = model.WorkName,
                    Unit = model.Unit,
                    Quantity = model.Quantity,
                    UnitPrice = model.UnitPrice,
                    TotalPrice = model.Quantity * model.UnitPrice,
                    Progress = model.Progress,
                    Status = ProgressStatusEnum.NotStarted,  // Default to NotStarted
                    PlanStartDate = model.PlanStartDate,
                    PlanEndDate = model.PlanEndDate,
                    ActualStartDate = model.ActualStartDate,
                    ActualEndDate = model.ActualEndDate,
                    UsedQuantity = 0, // Initialize as 0
                    ItemRelations = model.ItemRelations ?? new Dictionary<string, string>(),
                    Creator = actionBy,
                    Updater = actionBy,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                // Apply progress-based status rules
                if (model.Progress > 0 && model.Progress < 100)
                {
                    progressItem.Status = ProgressStatusEnum.InProgress;
                }
                else if (model.Progress == 100)
                {
                    progressItem.Status = ProgressStatusEnum.WaitForInspection;
                }
                else if (model.Progress == 0 && model.Status != 0)
                {
                    progressItem.Status = (ProgressStatusEnum)model.Status;
                }

                await _context.ConstructionProgressItems.AddAsync(progressItem);
                await _context.SaveChangesAsync();
                
                // Update parent progress if this item has a parent
                if (!string.IsNullOrEmpty(progressItem.ParentIndex))
                {
                    // Get all progress items for this progress to calculate parent progress
                    var allProgressItems = await _context.ConstructionProgressItems
                        .Where(pi => pi.ProgressId == model.ProgressId && !pi.Deleted)
                        .ToListAsync();
                        
                    // Create a set of indices to update
                    var updatedItemIndices = new HashSet<string> { progressItem.ParentIndex };
                    
                    // Update progress of parent items based on their children
                    await UpdateParentItemsProgress(allProgressItems, updatedItemIndices, actionBy);
                    
                    // Save changes after updating parent progress
                    await _context.SaveChangesAsync();
                }

                // Update project status based on all progress items
                await UpdateProjectStatusBasedOnProgress(progress.ProjectId, actionBy);
                
                await _context.SaveChangesAsync();

                // Commit transaction
                await transaction.CommitAsync();

                // Use comprehensive cache invalidation for all affected entities
                await InvalidateProgressItemRelatedCaches(progressItem.Id, progress.Id, progress.ProjectId, progress.PlanId);

                // Map to DTO and return
                var itemDto = new ProgressItemDTO
                {
                    Id = progressItem.Id,
                    WorkCode = progressItem.WorkCode,
                    Index = progressItem.Index,
                    ParentIndex = progressItem.ParentIndex,
                    WorkName = progressItem.WorkName,
                    Unit = progressItem.Unit,
                    Quantity = progressItem.Quantity,
                    UnitPrice = progressItem.UnitPrice,
                    TotalPrice = progressItem.TotalPrice,
                    Progress = progressItem.Progress,
                    Status = (int)progressItem.Status,
                    PlanStartDate = progressItem.PlanStartDate,
                    PlanEndDate = progressItem.PlanEndDate,
                    ActualStartDate = progressItem.ActualStartDate,
                    ActualEndDate = progressItem.ActualEndDate,
                    UsedQuantity = progressItem.UsedQuantity,
                    ItemRelations = progressItem.ItemRelations
                };

                return itemDto;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating construction progress item: {message}", ex.Message);
                throw;
            }
        }

        public async Task<ProgressItemDTO> UpdateProgressItem(UpdateProgressItemDTO model, int actionBy)
        {
            // Begin transaction
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                // Check if progress exists
                var progress = await _context.ConstructionProgresses
                    .Include(cp => cp.Project)
                    .FirstOrDefaultAsync(cp => cp.Id == model.ProgressId && !cp.Deleted);

                if (progress == null)
                {
                    throw new KeyNotFoundException(Message.ConstructionProgressMessage.NOT_FOUND);
                }

                // Check authorization - user must be a Technical Manager in the project
                var isUserTechnicalManager = await _context.ProjectUsers
                    .Include(pu => pu.User)
                    .AnyAsync(pu => 
                        pu.ProjectId == progress.ProjectId && 
                        pu.UserId == actionBy && 
                        pu.User.Role == RoleConstValue.TECHNICAL_MANAGER &&
                        !pu.Deleted);

                if (!isUserTechnicalManager && !_helperService.IsInRole(actionBy, RoleConstValue.EXECUTIVE_BOARD))
                {
                    throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED_PROJECT);
                }

                // Check if progress item exists
                var progressItem = await _context.ConstructionProgressItems
                    .FirstOrDefaultAsync(pi => pi.Id == model.Id && pi.ProgressId == model.ProgressId && !pi.Deleted);

                if (progressItem == null)
                {
                    throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
                }

                // Handle quantity update if provided
                if (model.Quantity.HasValue && model.Quantity.Value > 0)
                {
                    // Check if the new quantity is greater than used quantity
                    if (model.Quantity.Value < progressItem.UsedQuantity)
                    {
                        throw new ArgumentException(Message.ConstructionProgressMessage.INVALID_QUANTITY_UPDATE);
                    }
                    
                    // Update quantity
                    progressItem.Quantity = model.Quantity.Value;
                    
                    // Recalculate progress based on used quantity / total quantity
                    if (progressItem.Quantity > 0)
                    {
                        progressItem.Progress = (int)Math.Round((double)progressItem.UsedQuantity / (double)progressItem.Quantity * 100);
                        
                        // Cap progress at 100%
                        if (progressItem.Progress > 100)
                        {
                            progressItem.Progress = 100;
                        }
                    }
                    
                    // If status is InspectionFailed, change to InProgress when quantity is updated
                    if (progressItem.Status == ProgressStatusEnum.InspectionFailed)
                    {
                        progressItem.Status = ProgressStatusEnum.InProgress;
                        _logger.LogInformation($"Progress item {progressItem.Id} status changed from InspectionFailed to InProgress due to quantity update");
                    }
                }

                // Validate progress value
                if (model.Progress < 0 || model.Progress > 100)
                {
                    throw new ArgumentException(Message.ConstructionProgressMessage.INVALID_PROGRESS);
                }

                // Validate status
                if (!Enum.IsDefined(typeof(ProgressStatusEnum), model.Status))
                {
                    throw new ArgumentException(Message.ConstructionProgressMessage.INVALID_STATUS);
                }

                // Check if item is becoming completed
                bool becameCompleted = progressItem.Progress < 100 && model.Progress == 100;

                // Update progress item
                progressItem.Progress = model.Progress;
                // Update status based on progress value
                if (model.Progress > 0 && model.Progress < 100)
                {
                    progressItem.Status = ProgressStatusEnum.InProgress;
                }
                else if (model.Progress == 100)
                {
                    progressItem.Status = ProgressStatusEnum.WaitForInspection;
                }
                else
                {
                    progressItem.Status = (ProgressStatusEnum)model.Status;
                }
                
                // Handle specific status transitions
                if (model.Status == (int)ProgressStatusEnum.Paused)
                {
                    // Can only pause if currently in InProgress status
                    if (progressItem.Status != ProgressStatusEnum.InProgress)
                    {
                        throw new InvalidOperationException(Message.ConstructionProgressMessage.PAUSE_REQUIRES_IN_PROGRESS);
                    }
                    progressItem.Status = ProgressStatusEnum.Paused;
                }
                else if (model.Status == (int)ProgressStatusEnum.InProgress && progressItem.Status == ProgressStatusEnum.Paused)
                {
                    // Resuming from paused
                    progressItem.Status = ProgressStatusEnum.InProgress;
                }
                else if (model.Status == (int)ProgressStatusEnum.Cancelled)
                {
                    // When canceling, keep current progress but change status
                    progressItem.Status = ProgressStatusEnum.Cancelled;
                    
                    // Rollback materials (similar to inspection approval with Pass)
                    await RollbackMaterialsFromCompletedItem(progressItem.Id, progress.ProjectId, actionBy);
                    
                    // Invalidate resource inventory caches for this project
                    await InvalidateResourceInventoryCachesByProject(progress.ProjectId);
                }
                
                progressItem.ActualStartDate = model.ActualStartDate;
                progressItem.ActualEndDate = model.ActualEndDate;
                progressItem.PlanStartDate = model.PlanStartDate;
                progressItem.PlanEndDate = model.PlanEndDate;
                
                // Update item relations if provided
                if (model.ItemRelations != null)
                {
                    progressItem.ItemRelations = model.ItemRelations;
                }
                
                progressItem.Updater = actionBy;
                progressItem.UpdatedAt = DateTime.Now;

                _context.ConstructionProgressItems.Update(progressItem);

                // Process material rollback if item became completed
                if (becameCompleted)
                {
                    await RollbackMaterialsFromCompletedItem(progressItem.Id, progress.ProjectId, actionBy);
                }
                
                // Create a set of indices to update parent progress
                var updatedItemIndices = new HashSet<string> { progressItem.Index };
                
                // Add parent index if it exists
                if (!string.IsNullOrEmpty(progressItem.ParentIndex))
                {
                    updatedItemIndices.Add(progressItem.ParentIndex);
                }
                
                // Get all progress items for this progress to calculate parent progress
                var allProgressItems = await _context.ConstructionProgressItems
                    .Where(pi => pi.ProgressId == model.ProgressId && !pi.Deleted)
                    .ToListAsync();
                    
                // Update progress of parent items based on their children
                await UpdateParentItemsProgress(allProgressItems, updatedItemIndices, actionBy);
                
                // Ensure all parent progress updates are saved to the database
                await _context.SaveChangesAsync();

                // Update project status based on all progress items
                await UpdateProjectStatusBasedOnProgress(progress.ProjectId, actionBy);

                await _context.SaveChangesAsync();
                
                // Commit transaction
                await transaction.CommitAsync();

                // Use comprehensive cache invalidation for all affected entities
                await InvalidateProgressItemRelatedCaches(progressItem.Id, progress.Id, progress.ProjectId, progress.PlanId);

                // Map to DTO and return
                var itemDto = new ProgressItemDTO
                {
                    Id = progressItem.Id,
                    WorkCode = progressItem.WorkCode,
                    Index = progressItem.Index,
                    ParentIndex = progressItem.ParentIndex,
                    WorkName = progressItem.WorkName,
                    Unit = progressItem.Unit,
                    Quantity = progressItem.Quantity,
                    UnitPrice = progressItem.UnitPrice,
                    TotalPrice = progressItem.TotalPrice,
                    Progress = progressItem.Progress,
                    Status = (int)progressItem.Status,
                    PlanStartDate = progressItem.PlanStartDate,
                    PlanEndDate = progressItem.PlanEndDate,
                    ActualStartDate = progressItem.ActualStartDate,
                    ActualEndDate = progressItem.ActualEndDate,
                    UsedQuantity = progressItem.UsedQuantity,
                    ItemRelations = progressItem.ItemRelations
                };

                return itemDto;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating construction progress item: {message}", ex.Message);
                throw;
            }
        }

        // Helper method to generate unique WorkCode for construction progress items
        private async Task<string> GenerateUniqueWorkCode(string prefix, string index, int counter, Dictionary<string, int> subCounters, List<string> existingWorkCodes)
        {
            string workCode = string.Empty;
            bool workCodeExists = true;
            int attemptCounter = counter;

            // Keep trying until we find a unique work code
            while (workCodeExists)
            {
                // Generate work code based on index format
                if (index.Contains("."))
                {
                    // This is a sub-item (e.g., "1.1", "2.3", etc.)
                    string parentIndex = index.Split('.')[0];
                    
                    if (!subCounters.ContainsKey(parentIndex))
                    {
                        subCounters[parentIndex] = 1;
                    }
                    
                    int subCounter = subCounters[parentIndex]++;
                    workCode = $"{prefix}-{parentIndex}-{subCounter:D3}";
                }
                else
                {
                    // This is a main item
                    workCode = $"{prefix}-{attemptCounter:D3}";
                    attemptCounter++;
                }

                // Check if this work code already exists in our list of existing codes
                workCodeExists = existingWorkCodes.Contains(workCode);
            }

            return workCode;
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
                
                // Also clear construction progress caches related to this project
                await _cacheService.DeleteAsync(string.Format(RedisCacheKey.CONSTRUCTION_PROGRESS_BY_PROJECT_CACHE_KEY, projectId));
                
                // Clear general project-based caches for construction progress
                await _cacheService.DeleteAsync(RedisCacheKey.CONSTRUCTION_PROGRESS_CACHE_KEY);
            }
            catch (Exception ex)
            {
                // Log error but don't fail the operation
                _logger.LogError(ex, "Error invalidating project caches: {message}", ex.Message);
            }
        }

        private async Task InvalidateResourceInventoryCachesByProject(int projectId)
        {
            // Clear specific cache for this project's resource inventory
            await _cacheService.DeleteAsync("RESOURCE_INVENTORY:PROJECT:" + projectId);
            
            // Clear all resource inventory related caches
            await _cacheService.DeleteByPatternAsync("RESOURCE_INVENTORY:*");
        }

        /// <summary>
        /// A comprehensive cache invalidation method for ConstructionProgressItems and related entities
        /// </summary>
        /// <param name="progressItemId">The ID of the progress item</param>
        /// <param name="progressId">The ID of the construction progress</param>
        /// <param name="projectId">The ID of the project</param>
        /// <param name="planId">The ID of the construction plan</param>
        private async Task InvalidateProgressItemRelatedCaches(int progressItemId, int progressId, int projectId, int planId)
        {
            try
            {
                _logger.LogInformation($"Invalidating all related caches for progress item {progressItemId} in project {projectId}");
                
                // 1. Invalidate construction progress item caches - no specific constants for these, use patterns
                await _cacheService.DeleteByPatternAsync("ConstructionProgressItem:*");
                await _cacheService.DeleteByPatternAsync($"ConstructionProgressItem:Project:{projectId}:*");
                await _cacheService.DeleteByPatternAsync($"ConstructionProgressItem:ID:{progressItemId}");
                
                // 2. Invalidate construction progress detail caches
                await _cacheService.DeleteByPatternAsync("ConstructionProgressItemDetail:*");
                await _cacheService.DeleteByPatternAsync($"ConstructionProgressItemDetail:ProgressItem:{progressItemId}:*");
                
                // 3. Invalidate construction progress caches
                await InvalidateProgressCache(progressId, projectId, planId);
                
                // 4. Invalidate construction plan caches
                await _cacheService.DeleteAsync(RedisCacheKey.CONSTRUCTION_PLAN_CACHE_KEY);
                await _cacheService.DeleteByPatternAsync($"CONSTRUCTION_PLAN:*");
                
                // 5. Invalidate project caches
                await InvalidateProjectCaches(projectId);
                
                // 6. Invalidate inspection report caches that might depend on progress item status
                await _cacheService.DeleteAsync(RedisCacheKey.INSPECTION_REPORT_CACHE_KEY);
                await _cacheService.DeleteAsync(string.Format(RedisCacheKey.INSPECTION_REPORT_BY_PROJECT_CACHE_KEY, projectId));
                await _cacheService.DeleteByPatternAsync(RedisCacheKey.INSPECTION_REPORT_ALL_PATTERN);
                
                // 7. Invalidate resource inventory caches (materials/resources might be affected)
                await _cacheService.DeleteAsync(RedisCacheKey.RESOURCE_INVENTORY_CACHE_KEY);
                await _cacheService.DeleteByPatternAsync($"RESOURCE_INVENTORY:*");
                await _cacheService.DeleteByPatternAsync($"RESOURCE_INVENTORY:PROJECT:{projectId}:*");
                
                // 8. Invalidate construction log caches (they display progress data)
                await _cacheService.DeleteAsync(RedisCacheKey.CONSTRUCTION_LOG_CACHE_KEY);
                await _cacheService.DeleteAsync(string.Format(RedisCacheKey.CONSTRUCTION_LOG_BY_PROJECT_CACHE_KEY, projectId));
                await _cacheService.DeleteByPatternAsync(RedisCacheKey.CONSTRUCTION_LOG_ALL_PATTERN);
                
                // 9. Invalidate dashboard/statistics caches
                await _cacheService.DeleteByPatternAsync($"DASHBOARD:*");
                await _cacheService.DeleteByPatternAsync($"STATISTICS:*");
                await _cacheService.DeleteByPatternAsync($"REPORT:*");
                
                // 10. Invalidate project statistics cache
                await _cacheService.DeleteAsync(string.Format(RedisCacheKey.PROJECT_STATISTICS_PROJECT_CACHE_KEY, projectId));
                
                // 11. Invalidate vehicle caches if vehicle allocations are affected
                await _cacheService.DeleteByPatternAsync($"VEHICLE:*");
                await _cacheService.DeleteByPatternAsync($"MACHINE:*");
                
                _logger.LogInformation($"Cache invalidation completed for progress item {progressItemId}");
            }
            catch (Exception ex)
            {
                // Log error but don't fail the operation
                _logger.LogError(ex, $"Error during cache invalidation for progress item {progressItemId}: {ex.Message}");
            }
        }
    }
} 