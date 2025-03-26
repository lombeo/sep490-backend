using Microsoft.EntityFrameworkCore;
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
using Sep490_Backend.Services.HelperService;
using System.Text.RegularExpressions;

namespace Sep490_Backend.Services.ConstructionPlanService
{
    public class ConstructionPlanService : IConstructionPlanService
    {
        private readonly BackendContext _context;
        private readonly ICacheService _cacheService;
        private readonly IHelperService _helperService;

        public ConstructionPlanService(
            BackendContext context,
            ICacheService cacheService,
            IHelperService helperService)
        {
            _context = context;
            _cacheService = cacheService;
            _helperService = helperService;
        }

        public async Task<List<ConstructionPlanDTO>> Search(ConstructionPlanQuery query)
        {
            // Check if user is authorized to perform this action
            if (!_helperService.IsInRole(query.ActionBy, new List<string> 
            { 
                RoleConstValue.CONSTRUCTION_MANAGER, 
                RoleConstValue.TECHNICAL_MANAGER, 
                RoleConstValue.RESOURCE_MANAGER,
                RoleConstValue.EXECUTIVE_BOARD 
            }))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Create base query
            var constructionPlans = _context.ConstructionPlans
                .Include(cp => cp.Project)
                .Include(cp => cp.Reviewers)
                .Where(cp => !cp.Deleted);

            // Apply filters
            if (!string.IsNullOrEmpty(query.PlanName))
            {
                constructionPlans = constructionPlans.Where(cp => cp.PlanName.Contains(query.PlanName));
            }

            if (query.ProjectId.HasValue)
            {
                constructionPlans = constructionPlans.Where(cp => cp.ProjectId == query.ProjectId.Value);
            }

            if (query.FromDate.HasValue)
            {
                var fromDate = query.FromDate.Value.Date;
                constructionPlans = constructionPlans.Where(cp => cp.CreatedAt >= fromDate);
            }

            if (query.ToDate.HasValue)
            {
                var toDate = query.ToDate.Value.Date.AddDays(1).AddTicks(-1);
                constructionPlans = constructionPlans.Where(cp => cp.CreatedAt <= toDate);
            }

            if (query.IsApproved.HasValue)
            {
                if (query.IsApproved.Value)
                {
                    constructionPlans = constructionPlans.Where(cp => cp.Reviewer != null && cp.Reviewer.Count > 0 && cp.Reviewer.All(r => r.Value == true));
                }
                else
                {
                    constructionPlans = constructionPlans.Where(cp => cp.Reviewer == null || cp.Reviewer.Count == 0 || cp.Reviewer.Any(r => r.Value == false));
                }
            }

            // Order by creation date
            constructionPlans = constructionPlans.OrderByDescending(cp => cp.CreatedAt);

            // Apply pagination
            var totalCount = await constructionPlans.CountAsync();
            
            // Set pagination values
            query.Total = totalCount;
            int pageSize = query.PageSize == 0 ? 10 : query.PageSize;
            int skip = (query.PageIndex - 1) * pageSize;
            
            // Get results
            var entities = await constructionPlans
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync();

            // Map to DTOs
            var result = new List<ConstructionPlanDTO>();
            foreach (var entity in entities)
            {
                var creator = await _context.Users.FirstOrDefaultAsync(u => u.Id == entity.Creator);
                
                var dto = new ConstructionPlanDTO
                {
                    Id = entity.Id,
                    PlanName = entity.PlanName,
                    Reviewer = entity.Reviewer,
                    ProjectId = entity.ProjectId,
                    ProjectName = entity.Project?.ProjectName ?? "",
                    CreatedAt = entity.CreatedAt ?? DateTime.UtcNow,
                    UpdatedAt = entity.UpdatedAt ?? DateTime.UtcNow,
                    CreatedBy = entity.Creator,
                    CreatedByName = creator?.FullName ?? "",
                    UpdatedBy = entity.Updater,
                    IsApproved = entity.Reviewer != null && entity.Reviewer.Count > 0 && entity.Reviewer.All(r => r.Value == true)
                };

                // Add reviewers
                if (entity.Reviewers != null && entity.Reviewers.Any())
                {
                    foreach (var reviewer in entity.Reviewers)
                    {
                        bool isApproved = false;
                        if (entity.Reviewer != null && entity.Reviewer.ContainsKey(reviewer.Id))
                        {
                            isApproved = entity.Reviewer[reviewer.Id];
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

                result.Add(dto);
            }

            return result;
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

            // Create plan items if provided
            if (model.PlanItems != null && model.PlanItems.Any())
            {
                foreach (var itemDto in model.PlanItems)
                {
                    var planItem = new ConstructPlanItem
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
                        PlanQuantity = itemDto.PlanQuantity,
                        PlanTotalPrice = itemDto.PlanQuantity * itemDto.UnitPrice,
                        StartDate = itemDto.StartDate,
                        EndDate = itemDto.EndDate,
                        QA = itemDto.QAIds?.ToList() ?? new List<int>(),
                        ItemRelations = itemDto.ItemRelations ?? new Dictionary<string, string>(),
                        Creator = actionBy
                    };

                    // Add plan item
                    await _context.ConstructPlanItems.AddAsync(planItem);
                    
                    // Save to ensure the plan item exists before adding details
                    await _context.SaveChangesAsync();

                    // Add QA members
                    if (itemDto.QAIds != null && itemDto.QAIds.Any())
                    {
                        planItem.QAMembers = new List<User>();
                        foreach (var qaId in itemDto.QAIds)
                        {
                            var qaMember = await _context.Users.FirstOrDefaultAsync(u => u.Id == qaId && !u.Deleted);
                            if (qaMember != null)
                            {
                                planItem.QAMembers.Add(qaMember);
                            }
                        }
                    }

                    // Add teams if provided
                    if (itemDto.TeamIds != null && itemDto.TeamIds.Any())
                    {
                        planItem.ConstructionTeams = new List<ConstructionTeam>();
                        foreach (var teamId in itemDto.TeamIds)
                        {
                            var team = await _context.ConstructionTeams.FirstOrDefaultAsync(t => t.Id == teamId && !t.Deleted);
                            if (team != null)
                            {
                                planItem.ConstructionTeams.Add(team);
                            }
                        }
                    }

                    // Add details if provided
                    if (itemDto.Details != null && itemDto.Details.Any())
                    {
                        foreach (var detailDto in itemDto.Details)
                        {
                            var detail = new ConstructPlanItemDetail
                            {
                                PlanItemId = planItem.WorkCode,
                                WorkCode = detailDto.WorkCode,
                                ResourceType = detailDto.ResourceType,
                                Quantity = detailDto.Quantity,
                                Unit = detailDto.Unit,
                                UnitPrice = detailDto.UnitPrice,
                                Total = detailDto.Quantity * detailDto.UnitPrice,
                                Creator = actionBy
                            };

                            // Add detail
                            await _context.ConstructPlanItemDetails.AddAsync(detail);
                            
                            // Save to ensure the detail exists before adding resources
                            await _context.SaveChangesAsync();

                            // Add resources based on type
                            if (detailDto.ResourceIds != null && detailDto.ResourceIds.Any())
                            {
                                switch (detailDto.ResourceType)
                                {
                                    case ResourceType.HUMAN:
                                        detail.Users = new List<User>();
                                        foreach (var userId in detailDto.ResourceIds)
                                        {
                                            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.Deleted);
                                            if (user != null)
                                            {
                                                detail.Users.Add(user);
                                            }
                                        }
                                        break;

                                    case ResourceType.MACHINE:
                                        detail.Vehicles = new List<Vehicle>();
                                        foreach (var vehicleId in detailDto.ResourceIds)
                                        {
                                            var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId && !v.Deleted);
                                            if (vehicle != null)
                                            {
                                                detail.Vehicles.Add(vehicle);
                                            }
                                        }
                                        break;

                                    case ResourceType.MATERIAL:
                                        detail.Materials = new List<Material>();
                                        foreach (var materialId in detailDto.ResourceIds)
                                        {
                                            var material = await _context.Materials.FirstOrDefaultAsync(m => m.Id == materialId && !m.Deleted);
                                            if (material != null)
                                            {
                                                detail.Materials.Add(material);
                                            }
                                        }
                                        break;
                                }
                            }
                        }
                    }
                }

                // Final save
                await _context.SaveChangesAsync();
            }

            // Clear cache
            await _cacheService.DeleteAsync(RedisCacheKey.PROJECT_CACHE_KEY);

            // Return the created plan
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
            if (model.ReviewerIds != null)
            {
                // Create a new dictionary for reviewers
                var newReviewer = new Dictionary<int, bool>();
                
                // Transfer existing approval statuses
                if (constructionPlan.Reviewer != null)
                {
                    foreach (var reviewerId in model.ReviewerIds)
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
                    foreach (var reviewerId in model.ReviewerIds)
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
                
                foreach (var reviewerId in model.ReviewerIds)
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
                // Get all existing plan items
                var existingItems = await _context.ConstructPlanItems
                    .Where(pi => pi.PlanId == constructionPlan.Id)
                    .ToListAsync();

                // Create workcode set for fast lookup
                var existingWorkCodes = existingItems.Select(ei => ei.WorkCode).ToHashSet();
                var newWorkCodes = model.PlanItems.Select(pi => pi.WorkCode).ToHashSet();

                // Find items to remove
                var itemsToRemove = existingItems.Where(ei => !newWorkCodes.Contains(ei.WorkCode)).ToList();
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
                        planItem.PlanQuantity = itemDto.PlanQuantity;
                        planItem.PlanTotalPrice = itemDto.PlanQuantity * itemDto.UnitPrice;
                        planItem.StartDate = itemDto.StartDate;
                        planItem.EndDate = itemDto.EndDate;
                        planItem.QA = itemDto.QAIds?.ToList() ?? new List<int>();
                        planItem.ItemRelations = itemDto.ItemRelations ?? new Dictionary<string, string>();
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
                            PlanQuantity = itemDto.PlanQuantity,
                            PlanTotalPrice = itemDto.PlanQuantity * itemDto.UnitPrice,
                            StartDate = itemDto.StartDate,
                            EndDate = itemDto.EndDate,
                            QA = itemDto.QAIds?.ToList() ?? new List<int>(),
                            ItemRelations = itemDto.ItemRelations ?? new Dictionary<string, string>(),
                            Creator = actionBy
                        };
                        
                        await _context.ConstructPlanItems.AddAsync(planItem);
                    }
                    
                    // Save to ensure the plan item exists before updating details
                    await _context.SaveChangesAsync();
                    
                    // Update QA members
                    await UpdateQAMembers(planItem, itemDto.QAIds, actionBy);
                    
                    // Update teams
                    await UpdateTeams(planItem, itemDto.TeamIds, actionBy);
                    
                    // Update details
                    await UpdateItemDetails(planItem, itemDto.Details, actionBy);
                }
                
                // Final save
                await _context.SaveChangesAsync();
            }

            // Clear cache
            await _cacheService.DeleteAsync(RedisCacheKey.PROJECT_CACHE_KEY);

            // Return the updated plan
            return await GetById(constructionPlan.Id, actionBy);
        }

        private async Task UpdateQAMembers(ConstructPlanItem planItem, List<int>? qaIds, int actionBy)
        {
            // Load existing QA members if not already loaded
            if (planItem.QAMembers == null)
            {
                await _context.Entry(planItem)
                    .Collection(pi => pi.QAMembers)
                    .LoadAsync();
                
                if (planItem.QAMembers == null)
                {
                    planItem.QAMembers = new List<User>();
                }
            }
            
            // Clear existing QA members
            planItem.QAMembers.Clear();
            
            // Add new QA members
            if (qaIds != null && qaIds.Any())
            {
                foreach (var qaId in qaIds)
                {
                    var qaMember = await _context.Users.FirstOrDefaultAsync(u => u.Id == qaId && !u.Deleted);
                    if (qaMember != null)
                    {
                        planItem.QAMembers.Add(qaMember);
                    }
                }
            }
        }

        private async Task UpdateTeams(ConstructPlanItem planItem, List<int>? teamIds, int actionBy)
        {
            // Load existing teams if not already loaded
            if (planItem.ConstructionTeams == null)
            {
                await _context.Entry(planItem)
                    .Collection(pi => pi.ConstructionTeams)
                    .LoadAsync();
                
                if (planItem.ConstructionTeams == null)
                {
                    planItem.ConstructionTeams = new List<ConstructionTeam>();
                }
            }
            
            // Clear existing teams
            planItem.ConstructionTeams.Clear();
            
            // Add new teams
            if (teamIds != null && teamIds.Any())
            {
                foreach (var teamId in teamIds)
                {
                    var team = await _context.ConstructionTeams.FirstOrDefaultAsync(t => t.Id == teamId && !t.Deleted);
                    if (team != null)
                    {
                        planItem.ConstructionTeams.Add(team);
                    }
                }
            }
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
            foreach (var detailDto in details)
            {
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
                    detail.Updater = actionBy;
                    
                    _context.ConstructPlanItemDetails.Update(detail);
                    
                    // Save to ensure the detail exists before updating resources
                    await _context.SaveChangesAsync();
                    
                    // Update resources based on type
                    await UpdateDetailResources(detail, detailDto.ResourceType, detailDto.ResourceIds, actionBy);
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
                        Creator = actionBy
                    };
                    
                    await _context.ConstructPlanItemDetails.AddAsync(detail);
                    
                    // Save to ensure the detail exists before adding resources
                    await _context.SaveChangesAsync();
                    
                    // Add resources based on type
                    await UpdateDetailResources(detail, detailDto.ResourceType, detailDto.ResourceIds, actionBy);
                }
            }
        }

        private async Task UpdateDetailResources(ConstructPlanItemDetail detail, ResourceType resourceType, List<int>? resourceIds, int actionBy)
        {
            if (resourceIds == null || !resourceIds.Any())
            {
                return;
            }
            
            switch (resourceType)
            {
                case ResourceType.HUMAN:
                    // Load existing users if not already loaded
                    if (detail.Users == null)
                    {
                        await _context.Entry(detail)
                            .Collection(d => d.Users)
                            .LoadAsync();
                        
                        if (detail.Users == null)
                        {
                            detail.Users = new List<User>();
                        }
                    }
                    
                    // Clear existing users
                    detail.Users.Clear();
                    
                    // Add new users
                    foreach (var userId in resourceIds)
                    {
                        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.Deleted);
                        if (user != null)
                        {
                            detail.Users.Add(user);
                        }
                    }
                    break;

                case ResourceType.MACHINE:
                    // Load existing vehicles if not already loaded
                    if (detail.Vehicles == null)
                    {
                        await _context.Entry(detail)
                            .Collection(d => d.Vehicles)
                            .LoadAsync();
                        
                        if (detail.Vehicles == null)
                        {
                            detail.Vehicles = new List<Vehicle>();
                        }
                    }
                    
                    // Clear existing vehicles
                    detail.Vehicles.Clear();
                    
                    // Add new vehicles
                    foreach (var vehicleId in resourceIds)
                    {
                        var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId && !v.Deleted);
                        if (vehicle != null)
                        {
                            detail.Vehicles.Add(vehicle);
                        }
                    }
                    break;

                case ResourceType.MATERIAL:
                    // Load existing materials if not already loaded
                    if (detail.Materials == null)
                    {
                        await _context.Entry(detail)
                            .Collection(d => d.Materials)
                            .LoadAsync();
                        
                        if (detail.Materials == null)
                        {
                            detail.Materials = new List<Material>();
                        }
                    }
                    
                    // Clear existing materials
                    detail.Materials.Clear();
                    
                    // Add new materials
                    foreach (var materialId in resourceIds)
                    {
                        var material = await _context.Materials.FirstOrDefaultAsync(m => m.Id == materialId && !m.Deleted);
                        if (material != null)
                        {
                            detail.Materials.Add(material);
                        }
                    }
                    break;
            }
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
                .Include(pi => pi.QAMembers)
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
                    PlanQuantity = planItem.PlanQuantity,
                    PlanTotalPrice = planItem.PlanTotalPrice,
                    StartDate = planItem.StartDate,
                    EndDate = planItem.EndDate,
                    QA = planItem.QA,
                    ItemRelations = planItem.ItemRelations ?? new Dictionary<string, string>()
                };

                // Add QA members
                if (planItem.QAMembers != null && planItem.QAMembers.Any())
                {
                    foreach (var qaMember in planItem.QAMembers)
                    {
                        planItemDto.QAMembers.Add(new QAMemberDTO
                        {
                            Id = qaMember.Id,
                            Name = qaMember.FullName,
                            Email = qaMember.Email
                        });
                    }
                }

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
                    planItemDto.Details.Add(new ConstructPlanItemDetailDTO
                    {
                        Id = detail.Id,
                        PlanItemId = detail.PlanItemId,
                        WorkCode = detail.WorkCode,
                        ResourceType = detail.ResourceType.ToString(),
                        Quantity = detail.Quantity,
                        Unit = detail.Unit,
                        UnitPrice = detail.UnitPrice,
                        Total = detail.Total
                    });
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

            // Get construction plan
            var constructionPlan = await _context.ConstructionPlans
                .FirstOrDefaultAsync(cp => cp.Id == id && !cp.Deleted);

            if (constructionPlan == null)
            {
                throw new KeyNotFoundException(Message.ConstructionPlanMessage.NOT_FOUND);
            }

            // Mark construction plan as deleted
            constructionPlan.Deleted = true;
            constructionPlan.Updater = actionBy;
            _context.ConstructionPlans.Update(constructionPlan);

            // Soft delete all related plan items
            var planItems = await _context.ConstructPlanItems
                .Where(pi => pi.PlanId == id && !pi.Deleted)
                .ToListAsync();

            foreach (var planItem in planItems)
            {
                planItem.Deleted = true;
                planItem.Updater = actionBy;
                _context.ConstructPlanItems.Update(planItem);

                // Soft delete all related item details
                var itemDetails = await _context.ConstructPlanItemDetails
                    .Where(d => d.PlanItemId == planItem.WorkCode && !d.Deleted)
                    .ToListAsync();

                foreach (var detail in itemDetails)
                {
                    detail.Deleted = true;
                    detail.Updater = actionBy;
                    _context.ConstructPlanItemDetails.Update(detail);
                }
            }

            await _context.SaveChangesAsync();

            // Clear cache
            await _cacheService.DeleteAsync(RedisCacheKey.PROJECT_CACHE_KEY);

            return true;
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

            // Clear cache
            await _cacheService.DeleteAsync(RedisCacheKey.PROJECT_CACHE_KEY);

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

            // Clear cache
            await _cacheService.DeleteAsync(RedisCacheKey.PROJECT_CACHE_KEY);

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
                .FirstOrDefaultAsync(pi => pi.PlanId == model.PlanId && pi.WorkCode == model.WorkCode && !pi.Deleted);

            if (planItem == null)
            {
                throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
            }

            // Update teams
            await UpdateTeams(planItem, model.TeamIds, actionBy);
            await _context.SaveChangesAsync();

            // Clear cache
            await _cacheService.DeleteAsync(RedisCacheKey.PROJECT_CACHE_KEY);

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
                        
                        decimal planQuantity = 0;
                        if (decimal.TryParse(GetCellValue(row.GetCell(7)), out decimal parsedPlanQuantity))
                        {
                            planQuantity = parsedPlanQuantity;
                        }
                        
                        DateTime startDate = DateTime.Now;
                        if (DateTime.TryParse(GetCellValue(row.GetCell(8)), out DateTime parsedStartDate))
                        {
                            startDate = parsedStartDate;
                        }
                        
                        DateTime endDate = DateTime.Now.AddMonths(1);
                        if (DateTime.TryParse(GetCellValue(row.GetCell(9)), out DateTime parsedEndDate))
                        {
                            endDate = parsedEndDate;
                        }

                        // Create plan item
                        var planItem = new ConstructPlanItem
                        {
                            WorkCode = workCode,
                            Index = index,
                            PlanId = constructionPlan.Id,
                            ParentIndex = string.IsNullOrEmpty(parentIndex) ? null : parentIndex,
                            WorkName = workName,
                            Unit = unit,
                            Quantity = quantity,
                            UnitPrice = unitPrice,
                            TotalPrice = quantity * unitPrice,
                            PlanQuantity = planQuantity,
                            PlanTotalPrice = planQuantity * unitPrice,
                            StartDate = startDate,
                            EndDate = endDate,
                            QA = new List<int>(),
                            ItemRelations = new Dictionary<string, string>(),
                            Creator = actionBy
                        };

                        await _context.ConstructPlanItems.AddAsync(planItem);
                    }
                }

                await _context.SaveChangesAsync();
                
                // Clear cache
                await _cacheService.DeleteAsync(RedisCacheKey.PROJECT_CACHE_KEY);

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
    }
} 