using Microsoft.EntityFrameworkCore;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.ConstructionProgress;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Infra.Enums;
using Sep490_Backend.Services.CacheService;
using Sep490_Backend.Services.DataService;
using Sep490_Backend.Services.HelperService;

namespace Sep490_Backend.Services.ConstructionProgressService
{
    public interface IConstructionProgressService
    {
        Task<List<ConstructionProgressDTO>> Search(ConstructionProgressQuery query);
        Task<ConstructionProgressDTO> GetById(int id, int actionBy);
        Task<ConstructionProgressDTO> GetByProjectId(int projectId, int actionBy);
        Task<bool> Update(UpdateProgressItemsDTO model, int actionBy);
    }

    public class ConstructionProgressService : IConstructionProgressService
    {
        private readonly BackendContext _context;
        private readonly ICacheService _cacheService;
        private readonly IHelperService _helperService;
        private readonly IDataService _dataService;
        private readonly TimeSpan _cacheExpirationTime = TimeSpan.FromMinutes(30);

        public ConstructionProgressService(
            BackendContext context,
            ICacheService cacheService,
            IHelperService helperService,
            IDataService dataService)
        {
            _context = context;
            _cacheService = cacheService;
            _helperService = helperService;
            _dataService = dataService;
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
            // Check if progress exists
            var progress = await _context.ConstructionProgresses
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

                // Update progress item
                progressItem.Progress = updateItem.Progress;
                progressItem.Status = (ProgressStatusEnum)updateItem.Status;
                progressItem.ActualStartDate = updateItem.ActualStartDate;
                progressItem.ActualEndDate = updateItem.ActualEndDate;
                progressItem.Updater = actionBy;

                // If progress is 100%, set status to Done
                if (progressItem.Progress == 100)
                {
                    progressItem.Status = ProgressStatusEnum.Done;
                }

                _context.ConstructionProgressItems.Update(progressItem);
            }

            // Update progress entity
            progress.Updater = actionBy;
            _context.ConstructionProgresses.Update(progress);

            // Save changes
            await _context.SaveChangesAsync();

            // Clear cache
            await InvalidateProgressCache(progress.Id, progress.ProjectId, progress.PlanId);

            return true;
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
    }
} 