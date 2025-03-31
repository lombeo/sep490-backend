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
                        StartDate = itemDto.StartDate,
                        EndDate = itemDto.EndDate,
                        ItemRelations = ConvertItemRelationsToIndex(itemDto.ItemRelations) ?? new Dictionary<string, string>(),
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
                                ResourceId = detailDto.ResourceId,
                                Creator = actionBy
                            };

                            // Add detail
                            await _context.ConstructPlanItemDetails.AddAsync(detail);
                            
                            // Save to ensure the detail exists before adding resources
                            await _context.SaveChangesAsync();

                            // Set resource based on type if ResourceId is provided
                            if (detailDto.ResourceId.HasValue)
                            {
                                await SetDetailResource(detail, detailDto.ResourceType, detailDto.ResourceId.Value);
                            }
                        }
                    }
                }

                // Final save
                await _context.SaveChangesAsync();
            }

            // Clear cache
            await _cacheService.DeleteAsync(RedisCacheKey.CONSTRUCTION_PLAN_CACHE_KEY);
            // Clear user-specific caches
            var users = await _context.Users
                .Where(u => !u.Deleted)
                .Select(u => u.Id)
                .ToListAsync();
            foreach (var userId in users)
            {
                await _cacheService.DeleteAsync(string.Format(RedisCacheKey.CONSTRUCTION_PLAN_BY_USER_CACHE_KEY, userId));
            }

            // Return the created plan
            return await GetById(constructionPlan.Id, actionBy);
        }

        private async Task SetDetailResource(ConstructPlanItemDetail detail, ResourceType resourceType, int resourceId)
        {
            // ResourceId đã được thiết lập trong model, chỉ cần cập nhật các trường tham chiếu
            // để đảm bảo tương thích với truy vấn
            switch (resourceType)
            {
                case ResourceType.HUMAN:
                    // Đảm bảo rằng detail.ConstructionTeam được thiết lập cho loại HUMAN
                    var team = await _context.ConstructionTeams.FirstOrDefaultAsync(t => t.Id == resourceId && !t.Deleted);
                    if (team != null)
                    {
                        detail.ConstructionTeam = team;
                        // Đảm bảo các tham chiếu khác là null
                        detail.Vehicle = null;
                        detail.Material = null;
                        detail.User = null;
                    }
                    break;

                case ResourceType.MACHINE:
                    var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.Id == resourceId && !v.Deleted);
                    if (vehicle != null)
                    {
                        detail.Vehicle = vehicle;
                        // Đảm bảo các tham chiếu khác là null
                        detail.ConstructionTeam = null;
                        detail.Material = null;
                        detail.User = null;
                    }
                    break;

                case ResourceType.MATERIAL:
                    var material = await _context.Materials.FirstOrDefaultAsync(m => m.Id == resourceId && !m.Deleted);
                    if (material != null)
                    {
                        detail.Material = material;
                        // Đảm bảo các tham chiếu khác là null
                        detail.ConstructionTeam = null;
                        detail.Vehicle = null;
                        detail.User = null;
                    }
                    break;
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
                    
                    // Update QA members
                    await UpdateQAMembers(planItem, itemDto.QAIds, actionBy);
                    
                    // Update details
                    await UpdateItemDetails(planItem, itemDto.Details, actionBy);
                }
                
                // Final save
                await _context.SaveChangesAsync();
            }

            // Clear cache
            await _cacheService.DeleteAsync(RedisCacheKey.CONSTRUCTION_PLAN_CACHE_KEY);
            // Clear user-specific caches
            var users = await _context.Users
                .Where(u => !u.Deleted)
                .Select(u => u.Id)
                .ToListAsync();
            foreach (var userId in users)
            {
                await _cacheService.DeleteAsync(string.Format(RedisCacheKey.CONSTRUCTION_PLAN_BY_USER_CACHE_KEY, userId));
            }

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
                    detail.ResourceId = detailDto.ResourceId;
                    detail.Updater = actionBy;
                    
                    _context.ConstructPlanItemDetails.Update(detail);
                    
                    // Save to ensure the detail exists before updating resources
                    await _context.SaveChangesAsync();
                    
                    // Update resources based on type
                    await SetDetailResource(detail, detailDto.ResourceType, detailDto.ResourceId.Value);
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
                        ResourceId = detailDto.ResourceId,
                        Creator = actionBy
                    };
                    
                    await _context.ConstructPlanItemDetails.AddAsync(detail);
                    
                    // Save to ensure the detail exists before adding resources
                    await _context.SaveChangesAsync();
                    
                    // Add resources based on type
                    await SetDetailResource(detail, detailDto.ResourceType, detailDto.ResourceId.Value);
                }
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
                    StartDate = planItem.StartDate,
                    EndDate = planItem.EndDate,
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
            await _cacheService.DeleteAsync(RedisCacheKey.CONSTRUCTION_PLAN_CACHE_KEY);
            // Clear user-specific caches
            var users = await _context.Users
                .Where(u => !u.Deleted)
                .Select(u => u.Id)
                .ToListAsync();
            foreach (var userId in users)
            {
                await _cacheService.DeleteAsync(string.Format(RedisCacheKey.CONSTRUCTION_PLAN_BY_USER_CACHE_KEY, userId));
            }

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
            await _cacheService.DeleteAsync(RedisCacheKey.CONSTRUCTION_PLAN_CACHE_KEY);
            // Clear user-specific caches
            var users = await _context.Users
                .Where(u => !u.Deleted)
                .Select(u => u.Id)
                .ToListAsync();
            foreach (var userId in users)
            {
                await _cacheService.DeleteAsync(string.Format(RedisCacheKey.CONSTRUCTION_PLAN_BY_USER_CACHE_KEY, userId));
            }

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
            await _cacheService.DeleteAsync(RedisCacheKey.CONSTRUCTION_PLAN_CACHE_KEY);
            // Clear user-specific caches
            var users = await _context.Users
                .Where(u => !u.Deleted)
                .Select(u => u.Id)
                .ToListAsync();
            foreach (var userId in users)
            {
                await _cacheService.DeleteAsync(string.Format(RedisCacheKey.CONSTRUCTION_PLAN_BY_USER_CACHE_KEY, userId));
            }

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

            // Clear cache
            await _cacheService.DeleteAsync(RedisCacheKey.CONSTRUCTION_PLAN_CACHE_KEY);
            // Clear user-specific caches
            var users = await _context.Users
                .Where(u => !u.Deleted)
                .Select(u => u.Id)
                .ToListAsync();
            foreach (var userId in users)
            {
                await _cacheService.DeleteAsync(string.Format(RedisCacheKey.CONSTRUCTION_PLAN_BY_USER_CACHE_KEY, userId));
            }

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
                            WorkCode = workCode,
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
                
                // Clear cache
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
    }
} 