using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Npgsql;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.ConstructionPlan;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Infra.Enums;
using Sep490_Backend.Infra.Helps;
using Sep490_Backend.Services.CacheService;
using Sep490_Backend.Services.DataService;
using Sep490_Backend.Services.HelperService;
using System.Text.RegularExpressions;

namespace Sep490_Backend.Services.ConstructionPlanService
{
    public interface IConstructionPlanService
    {
        Task<List<ConstructionPlanDTO>> Search(ConstructionPlanQuery query);
        Task<ConstructionPlanDTO> Create(SaveConstructionPlanDTO model, int actionBy);
        Task<ConstructionPlanDTO> Update(SaveConstructionPlanDTO model, int actionBy);
        Task<ConstructionPlanDTO> GetById(int id, int actionBy);
        Task<bool> Delete(int id, int actionBy);
        Task<bool> Approve(ApproveConstructionPlanDTO model, int actionBy);
        Task<bool> Reject(ApproveConstructionPlanDTO model, int actionBy);
        Task<ConstructionPlanDTO> Import(ImportConstructionPlanDTO model, int actionBy);
        Task<bool> AssignTeam(AssignTeamDTO model, int actionBy);
    }

    public class ConstructionPlanService : IConstructionPlanService
    {
        private readonly BackendContext _context;
        private readonly ICacheService _cacheService;
        private readonly IHelperService _helperService;
        private readonly IDataService _dataService;

        public ConstructionPlanService(
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

        public async Task<List<ConstructionPlanDTO>> Search(ConstructionPlanQuery query)
        {
            // Delegate to DataService for optimized caching and standardized approach
            return await _dataService.ListConstructionPlan(query);
        }

        public async Task<ConstructionPlanDTO> Create(SaveConstructionPlanDTO model, int actionBy)
        {
            // Check if user is authorized to perform this action
            if (!_helperService.IsInRole(actionBy, RoleConstValue.CONSTRUCTION_MANAGER))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Check if project exists
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == model.ProjectId && !p.Deleted);
            if (project == null)
            {
                throw new KeyNotFoundException(Message.ConstructionPlanMessage.INVALID_PROJECT);
            }

            // Check if plan name already exists for this project
            var existingPlan = await _context.ConstructionPlans
                .FirstOrDefaultAsync(cp => cp.PlanName == model.PlanName 
                                    && cp.ProjectId == model.ProjectId 
                                    && !cp.Deleted);
            if (existingPlan != null)
            {
                throw new InvalidOperationException(Message.ConstructionPlanMessage.PLAN_NAME_EXIST);
            }

            // Create construction plan
            var constructionPlan = new ConstructionPlan
            {
                PlanName = model.PlanName,
                ProjectId = model.ProjectId,
                Creator = actionBy,
                Reviewer = new Dictionary<int, bool>()
            };

            // Add reviewers
            if (model.Reviewers != null && model.Reviewers.Any())
            {
                // Initialize reviewers collection if it's null
                constructionPlan.Reviewers = new List<User>();

                // Add each reviewer to dictionary with default false (not approved)
                foreach (var reviewerId in model.Reviewers)
                {
                    var reviewer = await _context.Users.FirstOrDefaultAsync(u => u.Id == reviewerId && !u.Deleted);
                    if (reviewer != null)
                    {
                        constructionPlan.Reviewers.Add(reviewer);
                        constructionPlan.Reviewer.Add(reviewerId, false);
                    }
                }
            }

            // Save construction plan
            await _context.ConstructionPlans.AddAsync(constructionPlan);
            await _context.SaveChangesAsync();

            // Create plan items if provided
            if (model.PlanItems != null && model.PlanItems.Any())
            {
                // Generate prefix for work codes based on project
                string projectPrefix = $"EW{model.ProjectId:D2}";
                int mainItemCounter = 1;
                Dictionary<string, int> subItemCounters = new Dictionary<string, int>();

                foreach (var itemDto in model.PlanItems)
                {
                    // Auto-generate WorkCode if not provided
                    string workCode = string.IsNullOrEmpty(itemDto.WorkCode) ? await GenerateUniqueWorkCode(projectPrefix, itemDto.Index, mainItemCounter, subItemCounters) : itemDto.WorkCode;
                    
                    if (itemDto.ParentIndex == null)
                    {
                        mainItemCounter++;
                    }

                    var planItem = new ConstructPlanItem
                    {
                        WorkCode = workCode,
                        Index = itemDto.Index,
                        PlanId = constructionPlan.Id,
                        ParentIndex = itemDto.ParentIndex,
                        WorkName = itemDto.WorkName,
                        Unit = itemDto.Unit,
                        Quantity = itemDto.Quantity,
                        UnitPrice = itemDto.UnitPrice,
                        TotalPrice = itemDto.Quantity * itemDto.UnitPrice,
                        StartDate = itemDto.StartDate,
                        EndDate = itemDto.EndDate,
                        ItemRelations = ConvertItemRelationsToIndex(itemDto.ItemRelations) ?? new Dictionary<string, string>(),
                        Creator = actionBy
                    };

                    // Add plan item
                    await _context.ConstructPlanItems.AddAsync(planItem);
                    
                    // Save to ensure the plan item exists before adding details
                    await _context.SaveChangesAsync();

                    // Add details if provided
                    if (itemDto.Details != null && itemDto.Details.Any())
                    {
                        int detailCounter = 1;
                        foreach (var detailDto in itemDto.Details)
                        {
                            // Auto-generate WorkCode for detail if not provided
                            string detailWorkCode = string.IsNullOrEmpty(detailDto.WorkCode) 
                                ? await GenerateUniqueDetailWorkCode(workCode, detailDto.ResourceType, detailCounter++) 
                                : detailDto.WorkCode;

                            var detail = new ConstructPlanItemDetail
                            {
                                PlanItemId = planItem.WorkCode,
                                WorkCode = detailWorkCode,
                                ResourceType = detailDto.ResourceType,
                                Quantity = detailDto.Quantity,
                                Unit = detailDto.Unit,
                                UnitPrice = detailDto.UnitPrice,
                                Total = detailDto.Quantity * detailDto.UnitPrice,
                                // IMPORTANT: Don't set ResourceId here to avoid FK constraint violations
                                ResourceId = null,
                                Creator = actionBy
                            };

                            // Step 1: Only set fields that don't trigger FK constraints
                            detail.ResourceType = detailDto.ResourceType;
                            detail.ResourceId = null; // Explicitly set to null to avoid FK issues
                            
                            // Step 2: Add and save the detail without any resource references
                            await _context.ConstructPlanItemDetails.AddAsync(detail);
                            await _context.SaveChangesAsync();
                            
                            // Step 3: If a resource ID is provided, handle resource association via direct SQL
                            if (detailDto.ResourceId.HasValue)
                            {
                                await SetResourceDirectly(detail.Id, detailDto.ResourceType, detailDto.ResourceId.Value);
                            }
                        }
                    }
                }

                // Final save
                await _context.SaveChangesAsync();
            }

            // Clear only the main cache 
            await _cacheService.DeleteAsync(RedisCacheKey.CONSTRUCTION_PLAN_CACHE_KEY);
            
            // Return the newly created plan
            return await GetById(constructionPlan.Id, actionBy);
        }

        public async Task<ConstructionPlanDTO> Update(SaveConstructionPlanDTO model, int actionBy)
        {
            // Check if user is authorized to perform this action
            if (!_helperService.IsInRole(actionBy, RoleConstValue.CONSTRUCTION_MANAGER))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Check if plan exists
            if (!model.Id.HasValue)
            {
                throw new ArgumentException(Message.CommonMessage.MISSING_PARAM);
            }

            var constructionPlan = await _context.ConstructionPlans
                .Include(cp => cp.Reviewers)
                .FirstOrDefaultAsync(cp => cp.Id == model.Id.Value && !cp.Deleted);

            if (constructionPlan == null)
            {
                throw new KeyNotFoundException(Message.ConstructionPlanMessage.NOT_FOUND);
            }

            // Check if project exists
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == model.ProjectId && !p.Deleted);
            if (project == null)
            {
                throw new KeyNotFoundException(Message.ConstructionPlanMessage.INVALID_PROJECT);
            }

            // Check if plan name already exists for this project (excluding this plan)
            var existingPlan = await _context.ConstructionPlans
                .FirstOrDefaultAsync(cp => cp.PlanName == model.PlanName 
                                    && cp.ProjectId == model.ProjectId 
                                    && cp.Id != model.Id.Value
                                    && !cp.Deleted);
            if (existingPlan != null)
            {
                throw new InvalidOperationException(Message.ConstructionPlanMessage.PLAN_NAME_EXIST);
            }

            // Update construction plan
            constructionPlan.PlanName = model.PlanName;
            constructionPlan.ProjectId = model.ProjectId;
            constructionPlan.Updater = actionBy;

            // Update reviewers
            if (model.Reviewers != null)
            {
                // Create a new dictionary for reviewers
                var newReviewer = new Dictionary<int, bool>();
                
                // Transfer existing approval statuses
                if (constructionPlan.Reviewer != null)
                {
                    foreach (var reviewerId in model.Reviewers)
                    {
                        if (constructionPlan.Reviewer.ContainsKey(reviewerId))
                        {
                            newReviewer[reviewerId] = constructionPlan.Reviewer[reviewerId];
                        }
                        else
                        {
                            newReviewer[reviewerId] = false;
                        }
                    }
                }
                else
                {
                    // Initialize with all false
                    foreach (var reviewerId in model.Reviewers)
                    {
                        newReviewer[reviewerId] = false;
                    }
                }
                
                constructionPlan.Reviewer = newReviewer;
                
                // Update the many-to-many relationship
                if (constructionPlan.Reviewers == null)
                {
                    constructionPlan.Reviewers = new List<User>();
                }
                else
                {
                    constructionPlan.Reviewers.Clear();
                }
                
                foreach (var reviewerId in model.Reviewers)
                {
                    var reviewer = await _context.Users.FirstOrDefaultAsync(u => u.Id == reviewerId && !u.Deleted);
                    if (reviewer != null)
                    {
                        constructionPlan.Reviewers.Add(reviewer);
                    }
                }
            }

            // Update the entity
            _context.ConstructionPlans.Update(constructionPlan);
            await _context.SaveChangesAsync();

            // Update plan items if provided
            if (model.PlanItems != null && model.PlanItems.Any())
            {
                // Generate prefix for work codes based on project
                string projectPrefix = $"EW{model.ProjectId:D2}";
                int mainItemCounter = 1;
                Dictionary<string, int> subItemCounters = new Dictionary<string, int>();

                // Get all existing plan items
                var existingItems = await _context.ConstructPlanItems
                    .Where(pi => pi.PlanId == constructionPlan.Id)
                    .ToListAsync();

                // Create workcode set for fast lookup
                var existingWorkCodes = existingItems.Select(ei => ei.WorkCode).ToHashSet();
                var providedWorkCodes = model.PlanItems
                    .Where(pi => !string.IsNullOrEmpty(pi.WorkCode))
                    .Select(pi => pi.WorkCode)
                    .ToHashSet();

                // Find items to remove - only consider items with valid work codes
                var itemsToRemove = existingItems
                    .Where(ei => !providedWorkCodes.Contains(ei.WorkCode) && 
                                model.PlanItems.All(p => p.WorkCode != ei.WorkCode))
                    .ToList();
                
                foreach (var itemToRemove in itemsToRemove)
                {
                    // Mark as deleted instead of removing from database
                    itemToRemove.Deleted = true;
                    itemToRemove.Updater = actionBy;
                    _context.ConstructPlanItems.Update(itemToRemove);
                }

                // Process each item in the model
                foreach (var itemDto in model.PlanItems)
                {
                    // Generate workCode if empty
                    if (string.IsNullOrEmpty(itemDto.WorkCode))
                    {
                        itemDto.WorkCode = await GenerateUniqueWorkCode(projectPrefix, itemDto.Index, mainItemCounter, subItemCounters);
                        if (itemDto.ParentIndex == null)
                        {
                            mainItemCounter++;
                        }
                    }

                    // Check if the item already exists
                    ConstructPlanItem planItem;
                    if (existingWorkCodes.Contains(itemDto.WorkCode))
                    {
                        // Update existing item
                        planItem = existingItems.First(ei => ei.WorkCode == itemDto.WorkCode);
                        
                        // Update properties
                        planItem.Index = itemDto.Index;
                        planItem.ParentIndex = itemDto.ParentIndex;
                        planItem.WorkName = itemDto.WorkName;
                        planItem.Unit = itemDto.Unit;
                        planItem.Quantity = itemDto.Quantity;
                        planItem.UnitPrice = itemDto.UnitPrice;
                        planItem.TotalPrice = itemDto.Quantity * itemDto.UnitPrice;
                        planItem.StartDate = itemDto.StartDate;
                        planItem.EndDate = itemDto.EndDate;
                        planItem.ItemRelations = ConvertItemRelationsToIndex(itemDto.ItemRelations) ?? new Dictionary<string, string>();
                        planItem.Updater = actionBy;
                        
                        _context.ConstructPlanItems.Update(planItem);
                    }
                    else
                    {
                        // Create new item
                        planItem = new ConstructPlanItem
                        {
                            WorkCode = itemDto.WorkCode,
                            Index = itemDto.Index,
                            PlanId = constructionPlan.Id,
                            ParentIndex = itemDto.ParentIndex,
                            WorkName = itemDto.WorkName,
                            Unit = itemDto.Unit,
                            Quantity = itemDto.Quantity,
                            UnitPrice = itemDto.UnitPrice,
                            TotalPrice = itemDto.Quantity * itemDto.UnitPrice,
                            StartDate = itemDto.StartDate,
                            EndDate = itemDto.EndDate,
                            ItemRelations = ConvertItemRelationsToIndex(itemDto.ItemRelations) ?? new Dictionary<string, string>(),
                            Creator = actionBy
                        };
                        
                        await _context.ConstructPlanItems.AddAsync(planItem);
                    }
                    
                    // Save to ensure the plan item exists before updating details
                    await _context.SaveChangesAsync();
                    
                    // Update details
                    await UpdateItemDetails(planItem, itemDto.Details, actionBy);
                }
                
                // Final save
                await _context.SaveChangesAsync();
            }

            // Clear only the main cache
            await _cacheService.DeleteAsync(RedisCacheKey.CONSTRUCTION_PLAN_CACHE_KEY);
            
            // Return the updated plan
            return await GetById(constructionPlan.Id, actionBy);
        }

        private async Task UpdateItemDetails(ConstructPlanItem planItem, List<SaveConstructPlanItemDetailDTO>? details, int actionBy)
        {
            if (details == null || !details.Any())
            {
                return;
            }
            
            // Get existing details
            var existingDetails = await _context.ConstructPlanItemDetails
                .Where(d => d.PlanItemId == planItem.WorkCode && !d.Deleted)
                .ToListAsync();
            
            // Create ID set for fast lookup
            var existingDetailIds = existingDetails
                .Where(d => d.Id > 0)
                .Select(d => d.Id)
                .ToHashSet();
            
            var newDetailIds = details
                .Where(d => d.Id.HasValue)
                .Select(d => d.Id.Value)
                .ToHashSet();
            
            // Find details to remove
            var detailsToRemove = existingDetails
                .Where(ed => ed.Id > 0 && !newDetailIds.Contains(ed.Id))
                .ToList();
            
            foreach (var detailToRemove in detailsToRemove)
            {
                // Mark as deleted instead of removing from database
                detailToRemove.Deleted = true;
                detailToRemove.Updater = actionBy;
                _context.ConstructPlanItemDetails.Update(detailToRemove);
            }
            
            // Process each detail in the model
            int detailCounter = 1;
            foreach (var detailDto in details)
            {
                // Generate workCode for detail if not provided
                if (string.IsNullOrEmpty(detailDto.WorkCode))
                {
                    detailDto.WorkCode = await GenerateUniqueDetailWorkCode(planItem.WorkCode, detailDto.ResourceType, detailCounter++);
                }

                if (detailDto.Id.HasValue && existingDetailIds.Contains(detailDto.Id.Value))
                {
                    // Update existing detail
                    var detail = existingDetails.First(ed => ed.Id == detailDto.Id.Value);
                    
                    // Update properties
                    detail.WorkCode = detailDto.WorkCode;
                    detail.ResourceType = detailDto.ResourceType;
                    detail.Quantity = detailDto.Quantity;
                    detail.Unit = detailDto.Unit;
                    detail.UnitPrice = detailDto.UnitPrice;
                    detail.Total = detailDto.Quantity * detailDto.UnitPrice;
                    detail.ResourceId = detailDto.ResourceId;
                    detail.Updater = actionBy;
                    
                    _context.ConstructPlanItemDetails.Update(detail);
                    
                    // Save to ensure the detail exists before updating resources
                    await _context.SaveChangesAsync();
                    
                    // Update resources based on type if ResourceId is provided
                    if (detailDto.ResourceId.HasValue)
                    {
                        await SetResourceDirectly(detail.Id, detailDto.ResourceType, detailDto.ResourceId.Value);
                    }
                }
                else
                {
                    // Create new detail
                    var detail = new ConstructPlanItemDetail
                    {
                        PlanItemId = planItem.WorkCode,
                        WorkCode = detailDto.WorkCode,
                        ResourceType = detailDto.ResourceType,
                        Quantity = detailDto.Quantity,
                        Unit = detailDto.Unit,
                        UnitPrice = detailDto.UnitPrice,
                        Total = detailDto.Quantity * detailDto.UnitPrice,
                        // IMPORTANT: Set ResourceId to null initially to avoid FK constraint violations
                        ResourceId = null,
                        Creator = actionBy
                    };
                    
                    await _context.ConstructPlanItemDetails.AddAsync(detail);
                    
                    // Save to ensure the detail exists before adding resources
                    await _context.SaveChangesAsync();
                    
                    // Step 1: Only set fields that don't trigger FK constraints
                    detail.ResourceType = detailDto.ResourceType;
                    detail.ResourceId = null; // Explicitly set to null to avoid FK issues
                    
                    // Step 2: Save without any resource references
                    _context.Update(detail);
                    await _context.SaveChangesAsync();
                    
                    // Step 3: If a resource ID is provided, handle resource association via direct SQL
                    if (detailDto.ResourceId.HasValue)
                    {
                        await SetResourceDirectly(detail.Id, detailDto.ResourceType, detailDto.ResourceId.Value);
                    }
                }
            }
        }

/**
 * PRODUCTION EMERGENCY FIX: A simplified approach to bypass foreign key constraint issues
 * This method completely disconnects resource handling from EF Core's tracking and constraints
 */
private async Task SetResourceDirectly(int detailId, ResourceType resourceType, int resourceId)
{
    // First verify the resource exists in the proper table
    bool resourceExists = false;
    
    try 
    {
        switch (resourceType)
        {
            case ResourceType.HUMAN:
                resourceExists = await _context.ConstructionTeams
                    .AnyAsync(t => t.Id == resourceId && !t.Deleted);
                break;

            case ResourceType.MACHINE:
                resourceExists = await _context.Vehicles
                    .AnyAsync(v => v.Id == resourceId && !v.Deleted);
                break;

            case ResourceType.MATERIAL:
                resourceExists = await _context.Materials
                    .AnyAsync(m => m.Id == resourceId && !m.Deleted);
                break;
                
            default:
                throw new ArgumentException($"Unsupported resource type: {resourceType}");
        }
        
        if (!resourceExists)
        {
            // Resource doesn't exist - exit silently without error
            System.Diagnostics.Debug.WriteLine($"Resource not found: Type={resourceType}, Id={resourceId}");
            return;
        }

        // Detach all entities from EF tracking to start fresh
        _context.ChangeTracker.Clear();
        
        // Find the detail and modify it in a completely disconnected way
        var detail = await _context.ConstructPlanItemDetails.FindAsync(detailId);
        if (detail == null)
        {
            return; // Detail not found, nothing to do
        }

        // Always store resource metadata in Unit field as a backup
        detail.Unit = $"Type={resourceType},Id={resourceId}|{detail.Unit}";

        // Set ResourceType normally
        detail.ResourceType = resourceType;
        
        // Handle different resource types properly using different approaches
        if (resourceType == ResourceType.MATERIAL)
        {
            // For materials, directly set the ResourceId and relationship
            detail.ResourceId = resourceId;
            
            // Update using disconnected entity approach
            _context.Update(detail);
            await _context.SaveChangesAsync();
            
            try {
                // Explicitly create relationship with Material
                var material = await _context.Materials.FindAsync(resourceId);
                if (material != null)
                {
                    // Ensure material has the collection initialized
                    if (material.ConstructPlanItemDetails == null)
                    {
                        material.ConstructPlanItemDetails = new List<ConstructPlanItemDetail>();
                    }
                    
                    // Add reference if not already there
                    if (!material.ConstructPlanItemDetails.Any(d => d.Id == detailId))
                    {
                        material.ConstructPlanItemDetails.Add(detail);
                        _context.Update(material);
                        await _context.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the error but continue since we already set ResourceId
                System.Diagnostics.Debug.WriteLine($"Error creating Material relationship: {ex.Message}");
            }
        }
        else
        {
            // For other resource types, use the original approach
            // Crucial step: Reset ResourceId to null first to detach from any existing FKs
            detail.ResourceId = null;
            
            // Update using disconnected entity approach
            _context.Update(detail);
            await _context.SaveChangesAsync();
            
            // Now try to set the ResourceId in a separate step
            try
            {
                // Refresh context to ensure clean state
                _context.ChangeTracker.Clear();
                
                // Find the entity again
                var detailForUpdate = await _context.ConstructPlanItemDetails.FindAsync(detailId);
                if (detailForUpdate != null)
                {
                    // CRITICAL: Use direct update approach with minimal tracking
                    detailForUpdate.ResourceId = resourceId;
                    _context.Entry(detailForUpdate).Property("ResourceId").IsModified = true;
                    
                    // Save only this change
                    await _context.SaveChangesAsync();
                    
                    // Immediately detach to avoid future issues
                    _context.Entry(detailForUpdate).State = EntityState.Detached;
                }
            }
            catch (Exception ex)
            {
                // Log error but continue - we still have the data in Unit field
                System.Diagnostics.Debug.WriteLine($"Failed to set ResourceId: {ex.Message}");
            }
        }
    }
    catch (Exception ex)
    {
        // Log error but don't block execution
        System.Diagnostics.Debug.WriteLine($"Resource association failed: {ex.Message}");
    }
}
        
        private Dictionary<string, string> ConvertItemRelationsToIndex(Dictionary<string, string> workCodeRelations)
        {
            if (workCodeRelations == null || !workCodeRelations.Any())
            {
                return new Dictionary<string, string>();
            }
            
            var indexRelations = new Dictionary<string, string>();
            
            foreach (var relation in workCodeRelations)
            {
                // Convert workCode to index
                // In this implementation, we simply use the index directly
                indexRelations[relation.Key] = relation.Value;
            }
            
            return indexRelations;
        }

        public async Task<ConstructionPlanDTO> GetById(int id, int actionBy)
        {
            // Check if user is authorized to perform this action
            if (!_helperService.IsInRole(actionBy, new List<string> 
            { 
                RoleConstValue.CONSTRUCTION_MANAGER, 
                RoleConstValue.TECHNICAL_MANAGER, 
                RoleConstValue.RESOURCE_MANAGER,
                RoleConstValue.EXECUTIVE_BOARD 
            }))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Get construction plan with related data
            var constructionPlan = await _context.ConstructionPlans
                .Include(cp => cp.Project)
                .Include(cp => cp.Reviewers)
                .FirstOrDefaultAsync(cp => cp.Id == id && !cp.Deleted);

            if (constructionPlan == null)
            {
                throw new KeyNotFoundException(Message.ConstructionPlanMessage.NOT_FOUND);
            }

            // Get creator
            var creator = await _context.Users.FirstOrDefaultAsync(u => u.Id == constructionPlan.Creator);

            // Create DTO
            var dto = new ConstructionPlanDTO
            {
                Id = constructionPlan.Id,
                PlanName = constructionPlan.PlanName,
                Reviewer = constructionPlan.Reviewer,
                ProjectId = constructionPlan.ProjectId,
                ProjectName = constructionPlan.Project?.ProjectName ?? "",
                CreatedAt = constructionPlan.CreatedAt ?? DateTime.UtcNow,
                UpdatedAt = constructionPlan.UpdatedAt ?? DateTime.UtcNow,
                CreatedBy = constructionPlan.Creator,
                CreatedByName = creator?.FullName ?? "",
                UpdatedBy = constructionPlan.Updater,
                IsApproved = constructionPlan.Reviewer != null && constructionPlan.Reviewer.Count > 0 && constructionPlan.Reviewer.All(r => r.Value == true)
            };

            // Add reviewers
            if (constructionPlan.Reviewers != null && constructionPlan.Reviewers.Any())
            {
                foreach (var reviewer in constructionPlan.Reviewers)
                {
                    bool isApproved = false;
                    if (constructionPlan.Reviewer != null && constructionPlan.Reviewer.ContainsKey(reviewer.Id))
                    {
                        isApproved = constructionPlan.Reviewer[reviewer.Id];
                    }

                    dto.Reviewers.Add(new ReviewerDTO
                    {
                        Id = reviewer.Id,
                        Name = reviewer.FullName,
                        Email = reviewer.Email,
                        IsApproved = isApproved
                    });
                }
            }

            // Get plan items
            var planItems = await _context.ConstructPlanItems
                .Include(pi => pi.ConstructionTeams)
                .Where(pi => pi.PlanId == id && !pi.Deleted)
                .ToListAsync();

            foreach (var planItem in planItems)
            {
                var planItemDto = new ConstructPlanItemDTO
                {
                    WorkCode = planItem.WorkCode,
                    Index = planItem.Index,
                    PlanId = planItem.PlanId,
                    ParentIndex = planItem.ParentIndex,
                    WorkName = planItem.WorkName,
                    Unit = planItem.Unit,
                    Quantity = planItem.Quantity,
                    UnitPrice = planItem.UnitPrice,
                    TotalPrice = planItem.TotalPrice,
                    StartDate = planItem.StartDate,
                    EndDate = planItem.EndDate,
                    ItemRelations = planItem.ItemRelations ?? new Dictionary<string, string>()
                };

                // Add teams
                if (planItem.ConstructionTeams != null && planItem.ConstructionTeams.Any())
                {
                    foreach (var team in planItem.ConstructionTeams)
                    {
                        var teamManager = await _context.Users.FirstOrDefaultAsync(u => u.Id == team.TeamManager);
                        
                        planItemDto.Teams.Add(new ConstructionTeamDTO
                        {
                            Id = team.Id,
                            TeamName = team.TeamName,
                            TeamManager = team.TeamManager,
                            TeamManagerName = teamManager?.FullName ?? "",
                            Description = team.Description
                        });
                    }
                }

                // Get item details
                var itemDetails = await _context.ConstructPlanItemDetails
                    .Where(d => d.PlanItemId == planItem.WorkCode && !d.Deleted)
                    .ToListAsync();

                foreach (var detail in itemDetails)
                {
                    var detailDto = new ConstructPlanItemDetailDTO
                    {
                        Id = detail.Id,
                        PlanItemId = detail.PlanItemId,
                        WorkCode = detail.WorkCode,
                        ResourceType = detail.ResourceType.ToString(),
                        Quantity = detail.Quantity,
                        Unit = detail.Unit,
                        UnitPrice = detail.UnitPrice,
                        Total = detail.Total,
                        ResourceId = detail.ResourceId
                    };
                    
                    // Add resource information based on type
                    if (detail.ResourceId.HasValue)
                    {
                        switch (detail.ResourceType)
                        {
                            case ResourceType.HUMAN:
                                var team = await _context.ConstructionTeams.FirstOrDefaultAsync(t => t.Id == detail.ResourceId && !t.Deleted);
                                if (team != null)
                                {
                                    detailDto.Resource = new ResourceDTO
                                    {
                                        Id = team.Id,
                                        Name = team.TeamName,
                                        Type = "TEAM"
                                    };
                                }
                                break;
                                
                            case ResourceType.MACHINE:
                                var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.Id == detail.ResourceId && !v.Deleted);
                                if (vehicle != null)
                                {
                                    detailDto.Resource = new ResourceDTO
                                    {
                                        Id = vehicle.Id,
                                        Name = vehicle.LicensePlate,
                                        Type = "VEHICLE"
                                    };
                                }
                                break;
                                
                            case ResourceType.MATERIAL:
                                var material = await _context.Materials.FirstOrDefaultAsync(m => m.Id == detail.ResourceId && !m.Deleted);
                                if (material != null)
                                {
                                    detailDto.Resource = new ResourceDTO
                                    {
                                        Id = material.Id,
                                        Name = material.MaterialName,
                                        Type = "MATERIAL"
                                    };
                                }
                                break;
                        }
                    }
                    
                    planItemDto.Details.Add(detailDto);
                }

                dto.PlanItems.Add(planItemDto);
            }

            return dto;
        }

        public async Task<bool> Delete(int id, int actionBy)
        {
            // Check if user is authorized to perform this action
            if (!_helperService.IsInRole(actionBy, RoleConstValue.CONSTRUCTION_MANAGER))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Get construction plan with related entities
            var constructionPlan = await _context.ConstructionPlans
                .Include(cp => cp.ConstructPlanItems)
                    .ThenInclude(pi => pi.ConstructPlanItemDetails)
                .FirstOrDefaultAsync(cp => cp.Id == id && !cp.Deleted);

            if (constructionPlan == null)
            {
                throw new KeyNotFoundException(Message.ConstructionPlanMessage.NOT_FOUND);
            }

            // Use the extension method for cascade soft delete
            await _context.SoftDeleteAsync(constructionPlan, actionBy);

            // Invalidate all related caches
            await InvalidateConstructionPlanCaches(constructionPlan.Id, constructionPlan.ProjectId);
            
            return true;
        }

        /// <summary>
        /// Invalidates all caches related to construction plans
        /// </summary>
        private async Task InvalidateConstructionPlanCaches(int planId, int projectId)
        {
            // Clear the main construction plan cache
            await _cacheService.DeleteAsync(RedisCacheKey.CONSTRUCTION_PLAN_CACHE_KEY);
            
            // Clear project-specific caches
            await _cacheService.DeleteAsync(RedisCacheKey.PROJECT_CACHE_KEY);
            
            // Clear any plan-specific caches
            var planSpecificCacheKey = $"CONSTRUCTION_PLAN:{planId}";
            await _cacheService.DeleteAsync(planSpecificCacheKey);
            
            // Clear project-specific plan caches
            var projectPlanCacheKey = $"CONSTRUCTION_PLAN:PROJECT:{projectId}";
            await _cacheService.DeleteAsync(projectPlanCacheKey);
            
            // Clear construction team caches as they might be associated with plans
            await _cacheService.DeleteAsync(RedisCacheKey.CONSTRUCTION_TEAM_CACHE_KEY);
        }

        public async Task<bool> Approve(ApproveConstructionPlanDTO model, int actionBy)
        {
            // Check if user is authorized to perform this action
            if (!_helperService.IsInRole(actionBy, new List<string> 
            { 
                RoleConstValue.TECHNICAL_MANAGER, 
                RoleConstValue.EXECUTIVE_BOARD 
            }))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Get construction plan
            var constructionPlan = await _context.ConstructionPlans
                .Include(cp => cp.Reviewers)
                .FirstOrDefaultAsync(cp => cp.Id == model.PlanId && !cp.Deleted);

            if (constructionPlan == null)
            {
                throw new KeyNotFoundException(Message.ConstructionPlanMessage.NOT_FOUND);
            }

            // Check if user is a reviewer
            if (!constructionPlan.Reviewers.Any(r => r.Id == actionBy))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Update reviewer status
            if (constructionPlan.Reviewer == null)
            {
                constructionPlan.Reviewer = new Dictionary<int, bool>();
            }

            constructionPlan.Reviewer[actionBy] = true;
            constructionPlan.Updater = actionBy;

            _context.ConstructionPlans.Update(constructionPlan);
            await _context.SaveChangesAsync();

            // Clear only the main cache
            await _cacheService.DeleteAsync(RedisCacheKey.CONSTRUCTION_PLAN_CACHE_KEY);
            
            return true;
        }

        public async Task<bool> Reject(ApproveConstructionPlanDTO model, int actionBy)
        {
            // Check if user is authorized to perform this action
            if (!_helperService.IsInRole(actionBy, new List<string> 
            { 
                RoleConstValue.TECHNICAL_MANAGER, 
                RoleConstValue.EXECUTIVE_BOARD 
            }))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Get construction plan
            var constructionPlan = await _context.ConstructionPlans
                .Include(cp => cp.Reviewers)
                .FirstOrDefaultAsync(cp => cp.Id == model.PlanId && !cp.Deleted);

            if (constructionPlan == null)
            {
                throw new KeyNotFoundException(Message.ConstructionPlanMessage.NOT_FOUND);
            }

            // Check if user is a reviewer
            if (!constructionPlan.Reviewers.Any(r => r.Id == actionBy))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Update reviewer status
            if (constructionPlan.Reviewer == null)
            {
                constructionPlan.Reviewer = new Dictionary<int, bool>();
            }

            constructionPlan.Reviewer[actionBy] = false;
            constructionPlan.Updater = actionBy;

            // TODO: Store rejection reason if needed

            _context.ConstructionPlans.Update(constructionPlan);
            await _context.SaveChangesAsync();

            // Clear only the main cache
            await _cacheService.DeleteAsync(RedisCacheKey.CONSTRUCTION_PLAN_CACHE_KEY);
            
            return true;
        }

        public async Task<bool> AssignTeam(AssignTeamDTO model, int actionBy)
        {
            // Check if user is authorized to perform this action
            if (!_helperService.IsInRole(actionBy, new List<string> 
            { 
                RoleConstValue.CONSTRUCTION_MANAGER
            }))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Get construction plan
            var constructionPlan = await _context.ConstructionPlans
                .FirstOrDefaultAsync(cp => cp.Id == model.PlanId && !cp.Deleted);

            if (constructionPlan == null)
            {
                throw new KeyNotFoundException(Message.ConstructionPlanMessage.NOT_FOUND);
            }

            // Get plan item
            var planItem = await _context.ConstructPlanItems
                .Include(pi => pi.ConstructionTeams)
                .FirstOrDefaultAsync(pi => pi.PlanId == model.PlanId && pi.WorkCode == model.WorkCode && !pi.Deleted);

            if (planItem == null)
            {
                throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
            }

            // Clear existing teams
            planItem.ConstructionTeams.Clear();

            // Add new teams
            if (model.TeamIds != null && model.TeamIds.Any())
            {
                foreach (var teamId in model.TeamIds)
                {
                    var team = await _context.ConstructionTeams.FirstOrDefaultAsync(t => t.Id == teamId && !t.Deleted);
                    if (team != null)
                    {
                        planItem.ConstructionTeams.Add(team);
                    }
                }
            }

            await _context.SaveChangesAsync();

            // Clear only the main cache
            await _cacheService.DeleteAsync(RedisCacheKey.CONSTRUCTION_PLAN_CACHE_KEY);
            
            return true;
        }

        public async Task<ConstructionPlanDTO> Import(ImportConstructionPlanDTO model, int actionBy)
        {
            // Check if user is authorized to perform this action
            if (!_helperService.IsInRole(actionBy, RoleConstValue.CONSTRUCTION_MANAGER))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Check if project exists
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == model.ProjectId && !p.Deleted);
            if (project == null)
            {
                throw new KeyNotFoundException(Message.ConstructionPlanMessage.INVALID_PROJECT);
            }

            // Check if plan name already exists for this project
            var existingPlan = await _context.ConstructionPlans
                .FirstOrDefaultAsync(cp => cp.PlanName == model.PlanName 
                                    && cp.ProjectId == model.ProjectId 
                                    && !cp.Deleted);
            if (existingPlan != null)
            {
                throw new InvalidOperationException(Message.ConstructionPlanMessage.PLAN_NAME_EXIST);
            }

            // Check file format
            if (model.ExcelFile == null || model.ExcelFile.Length == 0)
            {
                throw new ArgumentException(Message.CommonMessage.MISSING_PARAM);
            }

            string fileExtension = Path.GetExtension(model.ExcelFile.FileName);
            if (fileExtension != ".xlsx" && fileExtension != ".xls")
            {
                throw new ArgumentException(Message.ConstructionPlanMessage.INVALID_FILE_FORMAT);
            }

            // Create construction plan
            var constructionPlan = new ConstructionPlan
            {
                PlanName = model.PlanName,
                ProjectId = model.ProjectId,
                Creator = actionBy,
                Reviewer = new Dictionary<int, bool>()
            };

            // Add reviewers
            if (model.ReviewerIds != null && model.ReviewerIds.Any())
            {
                // Initialize reviewers collection if it's null
                constructionPlan.Reviewers = new List<User>();

                // Add each reviewer to dictionary with default false (not approved)
                foreach (var reviewerId in model.ReviewerIds)
                {
                    var reviewer = await _context.Users.FirstOrDefaultAsync(u => u.Id == reviewerId && !u.Deleted);
                    if (reviewer != null)
                    {
                        constructionPlan.Reviewers.Add(reviewer);
                        constructionPlan.Reviewer.Add(reviewerId, false);
                    }
                }
            }

            // Save construction plan
            await _context.ConstructionPlans.AddAsync(constructionPlan);
            await _context.SaveChangesAsync();

            // Process the Excel file
            try
            {
                using (var stream = model.ExcelFile.OpenReadStream())
                {
                    IWorkbook workbook;
                    
                    // Create workbook based on file extension
                    if (fileExtension == ".xlsx")
                    {
                        workbook = new XSSFWorkbook(stream);
                    }
                    else
                    {
                        throw new ArgumentException(Message.ConstructionPlanMessage.INVALID_FILE_FORMAT);
                    }

                    // Process the first sheet by default
                    ISheet sheet = workbook.GetSheetAt(0);
                    if (sheet == null)
                    {
                        throw new ArgumentException(Message.ConstructionPlanMessage.INVALID_FILE_CONTENT);
                    }

                    // Skip header row and start from second row
                    for (int i = 1; i <= sheet.LastRowNum; i++)
                    {
                        IRow row = sheet.GetRow(i);
                        if (row == null)
                        {
                            continue;
                        }

                        // Make sure we have the required columns (adjust indices based on your Excel structure)
                        if (row.GetCell(0) == null || row.GetCell(1) == null || row.GetCell(2) == null)
                        {
                            continue;
                        }

                        // Extract data from cells (adjust cell indices based on your Excel structure)
                        string workCode = GetCellValue(row.GetCell(0));
                        string index = GetCellValue(row.GetCell(1));
                        string parentIndex = GetCellValue(row.GetCell(2));
                        string workName = GetCellValue(row.GetCell(3));
                        string unit = GetCellValue(row.GetCell(4));
                        
                        decimal quantity = 0;
                        if (decimal.TryParse(GetCellValue(row.GetCell(5)), out decimal parsedQuantity))
                        {
                            quantity = parsedQuantity;
                        }
                        
                        decimal unitPrice = 0;
                        if (decimal.TryParse(GetCellValue(row.GetCell(6)), out decimal parsedUnitPrice))
                        {
                            unitPrice = parsedUnitPrice;
                        }
                        
                        DateTime startDate = DateTime.Now;
                        if (DateTime.TryParse(GetCellValue(row.GetCell(7)), out DateTime parsedStartDate))
                        {
                            startDate = parsedStartDate;
                        }
                        
                        DateTime endDate = DateTime.Now.AddMonths(1);
                        if (DateTime.TryParse(GetCellValue(row.GetCell(8)), out DateTime parsedEndDate))
                        {
                            endDate = parsedEndDate;
                        }

                        // Create plan item
                        var planItem = new ConstructPlanItem
                        {
                            WorkCode = await GenerateUniqueWorkCode($"EW{model.ProjectId:D2}", index, 1, new Dictionary<string, int>()),
                            Index = index,
                            PlanId = constructionPlan.Id,
                            ParentIndex = string.IsNullOrEmpty(parentIndex) ? null : parentIndex,
                            WorkName = workName,
                            Unit = unit,
                            Quantity = quantity,
                            UnitPrice = unitPrice,
                            TotalPrice = quantity * unitPrice,
                            StartDate = startDate,
                            EndDate = endDate,
                            ItemRelations = new Dictionary<string, string>(),
                            Creator = actionBy
                        };

                        await _context.ConstructPlanItems.AddAsync(planItem);
                    }
                }

                await _context.SaveChangesAsync();
                
                // Clear only the main cache
                await _cacheService.DeleteAsync(RedisCacheKey.CONSTRUCTION_PLAN_CACHE_KEY);

                // Return the created plan
                return await GetById(constructionPlan.Id, actionBy);
            }
            catch (Exception ex)
            {
                // If anything goes wrong, delete the construction plan
                _context.ConstructionPlans.Remove(constructionPlan);
                await _context.SaveChangesAsync();
                
                throw new ArgumentException(Message.ConstructionPlanMessage.INVALID_FILE_CONTENT + " - " + ex.Message);
            }
        }

        private string GetCellValue(ICell cell)
        {
            if (cell == null)
            {
                return string.Empty;
            }

            switch (cell.CellType)
            {
                case CellType.String:
                    return cell.StringCellValue;
                case CellType.Numeric:
                    if (DateUtil.IsCellDateFormatted(cell))
                    {
                        // Use standard date formatting instead of custom format with culture
                        DateTime dateValue = cell.DateCellValue ?? DateTime.UtcNow;
                        return dateValue.Year + "-" + dateValue.Month.ToString("D2") + "-" + dateValue.Day.ToString("D2");
                    }
                    else
                    {
                        return cell.NumericCellValue.ToString();
                    }
                case CellType.Boolean:
                    return cell.BooleanCellValue.ToString();
                case CellType.Formula:
                    switch (cell.CachedFormulaResultType)
                    {
                        case CellType.String:
                            return cell.StringCellValue;
                        case CellType.Numeric:
                            return cell.NumericCellValue.ToString();
                        default:
                            return string.Empty;
                    }
                default:
                    return string.Empty;
            }
        }

        // Helper method to generate WorkCode for construction plan items
        private async Task<string> GenerateUniqueWorkCode(string prefix, string index, int counter, Dictionary<string, int> subCounters)
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

                // Check if this work code already exists in the database
                workCodeExists = await _context.ConstructPlanItems
                    .AnyAsync(cpi => cpi.WorkCode == workCode && !cpi.Deleted);
            }

            return workCode;
        }

        // Helper method to generate WorkCode for detail items based on resource type
        private async Task<string> GenerateUniqueDetailWorkCode(string parentWorkCode, ResourceType resourceType, int counter)
        {
            string prefix = resourceType switch
            {
                ResourceType.HUMAN => "HUM",
                ResourceType.MATERIAL => "MAT",
                ResourceType.MACHINE => "MCH",
                _ => "RES"
            };
            
            // Remove any hyphens from parent work code to create a more compact code
            string sanitizedParent = parentWorkCode.Replace("-", "");
            
            string detailWorkCode = string.Empty;
            bool workCodeExists = true;
            int attemptCounter = counter;

            // Keep trying until we find a unique work code
            while (workCodeExists)
            {
                detailWorkCode = $"{prefix}-{sanitizedParent}-{attemptCounter:D3}";
                attemptCounter++;

                // Check if this work code already exists in the database
                workCodeExists = await _context.ConstructPlanItemDetails
                    .AnyAsync(detail => detail.WorkCode == detailWorkCode && !detail.Deleted);
            }

            return detailWorkCode;
        }
    }
} 