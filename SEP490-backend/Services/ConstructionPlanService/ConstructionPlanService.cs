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

            // Verify if user is part of the project
            var isUserInProject = await _context.ProjectUsers
                .AnyAsync(pu => pu.ProjectId == model.ProjectId && pu.UserId == actionBy && !pu.Deleted);
            if (!isUserInProject)
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED_PROJECT);
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
                Reviewer = new Dictionary<int, bool?>() // Changed to nullable bool to support null values
            };

            // Initialize reviewers collection
            constructionPlan.Reviewers = new List<User>();

            // Find the Technical Manager for this project
            var technicalManager = await _context.ProjectUsers
                .Include(pu => pu.User)
                .Where(pu => pu.ProjectId == model.ProjectId 
                      && !pu.Deleted 
                      && pu.User.Role == RoleConstValue.TECHNICAL_MANAGER 
                      && !pu.User.Deleted)
                .Select(pu => pu.User)
                .FirstOrDefaultAsync();

            // Find the Resource Manager for this project
            var resourceManager = await _context.ProjectUsers
                .Include(pu => pu.User)
                .Where(pu => pu.ProjectId == model.ProjectId 
                      && !pu.Deleted 
                      && pu.User.Role == RoleConstValue.RESOURCE_MANAGER 
                      && !pu.User.Deleted)
                .Select(pu => pu.User)
                .FirstOrDefaultAsync();

            // Find the Executive Board member
            var executiveBoard = await _context.Users
                .Where(u => u.Role == RoleConstValue.EXECUTIVE_BOARD && !u.Deleted)
                .FirstOrDefaultAsync();

            // Add the Technical Manager as reviewer if found
            if (technicalManager != null)
            {
                constructionPlan.Reviewers.Add(technicalManager);
                constructionPlan.Reviewer.Add(technicalManager.Id, null); // Initialize as null
            }
            
            // Add the Resource Manager as reviewer if found
            if (resourceManager != null && (technicalManager == null || technicalManager.Id != resourceManager.Id))
            {
                constructionPlan.Reviewers.Add(resourceManager);
                constructionPlan.Reviewer.Add(resourceManager.Id, null); // Initialize as null
            }

            // Add the Executive Board member as reviewer if found
            if (executiveBoard != null && 
                (technicalManager == null || technicalManager.Id != executiveBoard.Id) &&
                (resourceManager == null || resourceManager.Id != executiveBoard.Id))
            {
                constructionPlan.Reviewers.Add(executiveBoard);
                constructionPlan.Reviewer.Add(executiveBoard.Id, null); // Initialize as null
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
                                PlanItemId = Convert.ToInt32(planItem.Id),
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

            // Clear caches - using improved cache invalidation
            await InvalidateConstructionPlanCaches(constructionPlan.Id, constructionPlan.ProjectId);
            
            // Return the newly created plan
            return await GetById(constructionPlan.Id, actionBy);
        }

        public async Task<ConstructionPlanDTO> Update(SaveConstructionPlanDTO model, int actionBy)
        {
            // Check if user is authorized to perform this action
            if (!_helperService.IsInRole(actionBy, new List<string> {
                RoleConstValue.CONSTRUCTION_MANAGER,
                RoleConstValue.TECHNICAL_MANAGER,
                RoleConstValue.RESOURCE_MANAGER
            }))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Check if plan exists
            if (!model.Id.HasValue)
            {
                throw new ArgumentException(Message.CommonMessage.MISSING_PARAM);
            }
            
            // Start with a clean tracking context to avoid issues
            _context.ChangeTracker.Clear();

            // Handle ConstructionPlan entity without reviewers first
            var constructionPlan = await _context.ConstructionPlans
                .AsNoTracking() // Important: Use AsNoTracking to avoid tracking issues
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

            // Verify if user is part of the project (except for Executive Board who can access all)
            if (!_helperService.IsInRole(actionBy, RoleConstValue.EXECUTIVE_BOARD))
            {
                var isUserInProject = await _context.ProjectUsers
                    .AnyAsync(pu => pu.ProjectId == model.ProjectId && pu.UserId == actionBy && !pu.Deleted);
                if (!isUserInProject)
                {
                    throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED_PROJECT);
                }
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

            // Update construction plan basic properties
            constructionPlan.PlanName = model.PlanName;
            constructionPlan.ProjectId = model.ProjectId;
            constructionPlan.Updater = actionBy;

            // Initialize or preserve the Reviewer dictionary
            if (constructionPlan.Reviewer == null)
            {
                constructionPlan.Reviewer = new Dictionary<int, bool?>();
            }

            try 
            {
                // Start a transaction for atomic operations
                using var transaction = await _context.Database.BeginTransactionAsync();
                
                try
                {
                    // Step 1: Update the main entity first without touching relationships
                    _context.ConstructionPlans.Update(constructionPlan);
                    await _context.SaveChangesAsync();
                    
                    // Clear tracking again to ensure clean state
                    _context.ChangeTracker.Clear();
                    
                    // Get current reviewers for the plan
                    List<int> currentReviewerIds = new List<int>();
                    List<User> newReviewers = new List<User>();
                    
                    // Step 2: Handle reviewers if project changed
                    if (constructionPlan.ProjectId != model.ProjectId)
                    {
                        // Get the current reviewer IDs using direct SQL to avoid tracking issues
                        using (var conn = new NpgsqlConnection(_context.Database.GetConnectionString()))
                        {
                            await conn.OpenAsync();
                            using var cmd = new NpgsqlCommand(
                                @"SELECT ""ReviewerId"" FROM ""ConstructionPlanReviewers"" 
                                  WHERE ""ReviewedPlanId"" = @planId", conn);
                            cmd.Parameters.AddWithValue("planId", constructionPlan.Id);
                            
                            using var reader = await cmd.ExecuteReaderAsync();
                            while (await reader.ReadAsync())
                            {
                                currentReviewerIds.Add(reader.GetInt32(0));
                            }
                        }
                        
                        // Find the Technical Manager for the new project
                        var technicalManager = await _context.ProjectUsers
                            .Include(pu => pu.User)
                            .Where(pu => pu.ProjectId == model.ProjectId 
                                  && !pu.Deleted 
                                  && pu.User.Role == RoleConstValue.TECHNICAL_MANAGER 
                                  && !pu.User.Deleted)
                            .Select(pu => pu.User)
                            .FirstOrDefaultAsync();
                        
                        // Find the Resource Manager for the new project
                        var resourceManager = await _context.ProjectUsers
                            .Include(pu => pu.User)
                            .Where(pu => pu.ProjectId == model.ProjectId 
                                  && !pu.Deleted 
                                  && pu.User.Role == RoleConstValue.RESOURCE_MANAGER 
                                  && !pu.User.Deleted)
                            .Select(pu => pu.User)
                            .FirstOrDefaultAsync();
                        
                        // Find Executive Board member (either existing or new)
                        var executiveBoard = await _context.Users
                            .Where(u => u.Role == RoleConstValue.EXECUTIVE_BOARD && !u.Deleted)
                            .FirstOrDefaultAsync();
                        
                        // Build new reviewer list based on role
                        if (technicalManager != null && !currentReviewerIds.Contains(technicalManager.Id))
                        {
                            newReviewers.Add(technicalManager);
                            constructionPlan.Reviewer[technicalManager.Id] = null;
                        }
                        
                        if (resourceManager != null && !currentReviewerIds.Contains(resourceManager.Id))
                        {
                            newReviewers.Add(resourceManager);
                            constructionPlan.Reviewer[resourceManager.Id] = null;
                        }
                        
                        if (executiveBoard != null && !currentReviewerIds.Contains(executiveBoard.Id))
                        {
                            newReviewers.Add(executiveBoard);
                            constructionPlan.Reviewer[executiveBoard.Id] = null;
                        }
                        
                        // Step 3: Update the ConstructionPlanReviewers table directly with SQL
                        // First, delete reviewers with specific roles (Technical Manager, Resource Manager)
                        using (var conn = new NpgsqlConnection(_context.Database.GetConnectionString()))
                        {
                            await conn.OpenAsync();
                            
                            // Delete existing Technical and Resource managers
                            using (var cmd = new NpgsqlCommand(
                                @"DELETE FROM ""ConstructionPlanReviewers"" 
                                  WHERE ""ReviewedPlanId"" = @planId AND ""ReviewerId"" IN (
                                      SELECT ""Id"" FROM ""Users"" 
                                      WHERE ""Role"" IN ('TECHNICAL_MANAGER', 'RESOURCE_MANAGER')
                                  )", conn))
                            {
                                cmd.Parameters.AddWithValue("planId", constructionPlan.Id);
                                await cmd.ExecuteNonQueryAsync();
                            }
                            
                            // Add new reviewers
                            foreach (var reviewer in newReviewers)
                            {
                                using (var insertCmd = new NpgsqlCommand(
                                    @"INSERT INTO ""ConstructionPlanReviewers"" (""ReviewedPlanId"", ""ReviewerId"")
                                      VALUES (@planId, @reviewerId)
                                      ON CONFLICT (""ReviewedPlanId"", ""ReviewerId"") DO NOTHING", conn))
                                {
                                    insertCmd.Parameters.AddWithValue("planId", constructionPlan.Id);
                                    insertCmd.Parameters.AddWithValue("reviewerId", reviewer.Id);
                                    await insertCmd.ExecuteNonQueryAsync();
                                }
                            }
                        }
                        
                        // Step 4: Update Reviewer dictionary field in ConstructionPlans
                        using (var conn = new NpgsqlConnection(_context.Database.GetConnectionString()))
                        {
                            await conn.OpenAsync();
                            using (var cmd = new NpgsqlCommand(
                                @"UPDATE ""ConstructionPlans"" 
                                  SET ""Reviewer"" = @reviewer
                                  WHERE ""Id"" = @planId", conn))
                            {
                                cmd.Parameters.AddWithValue("planId", constructionPlan.Id);
                                cmd.Parameters.AddWithValue("reviewer", 
                                    Newtonsoft.Json.JsonConvert.SerializeObject(constructionPlan.Reviewer));
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }
                    }
                    
                    // Commit transaction
                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    // Rollback if any errors occur
                    await transaction.RollbackAsync();
                    throw new DbUpdateException($"Failed to update ConstructionPlan: {ex.Message}", ex);
                }
            }
            catch (Exception ex)
            {
                // Log the exception
                System.Diagnostics.Debug.WriteLine($"Error updating ConstructionPlan: {ex.Message}");
                throw;
            }

            // Update plan items if provided, with clean tracking context
            _context.ChangeTracker.Clear();
            
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

            // Clear caches - using improved cache invalidation
            await InvalidateConstructionPlanCaches(constructionPlan.Id, constructionPlan.ProjectId);
            
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
                .Where(d => d.PlanItemId == planItem.Id && !d.Deleted)
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
            
            // Save changes for deletions first and clear the tracker
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();
            
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
                    // Update existing detail - first clear tracker to avoid conflicts
                    _context.ChangeTracker.Clear();
                    
                    // Fetch the entity fresh from the database
                    var detail = await _context.ConstructPlanItemDetails.FindAsync(detailDto.Id.Value);
                    
                    if (detail != null)
                    {
                        // Update properties
                        detail.WorkCode = detailDto.WorkCode;
                        detail.ResourceType = detailDto.ResourceType;
                        detail.Quantity = detailDto.Quantity;
                        detail.Unit = detailDto.Unit;
                        detail.UnitPrice = detailDto.UnitPrice;
                        detail.Total = detailDto.Quantity * detailDto.UnitPrice;
                        
                        // Important: Set ResourceId to null first to avoid FK constraint issues
                        detail.ResourceId = null;
                        detail.Updater = actionBy;
                        
                        _context.Update(detail);
                        
                        // Save to ensure the detail exists before updating resources
                        await _context.SaveChangesAsync();
                        
                        // Update resources based on type if ResourceId is provided
                        if (detailDto.ResourceId.HasValue)
                        {
                            await SetResourceDirectly(detail.Id, detailDto.ResourceType, detailDto.ResourceId.Value);
                        }
                    }
                }
                else
                {
                    // Clear tracking before creating a new entity
                    _context.ChangeTracker.Clear();
                    
                    // Create new detail
                    var detail = new ConstructPlanItemDetail
                    {
                        PlanItemId = Convert.ToInt32(planItem.Id),
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
                    
                    // Clear tracker again before handling resources to avoid conflicts
                    _context.ChangeTracker.Clear();
                    
                    // If a resource ID is provided, handle resource association
                    if (detailDto.ResourceId.HasValue)
                    {
                        await SetResourceDirectly(detail.Id, detailDto.ResourceType, detailDto.ResourceId.Value);
                    }
                }
            }
            
            // Final clear of tracker to avoid future issues
            _context.ChangeTracker.Clear();
        }

/**
 * PRODUCTION EMERGENCY FIX: A simplified approach to bypass foreign key constraint issues
 * This method completely disconnects resource handling from EF Core's tracking and constraints
 */
private async Task SetResourceDirectly(int detailId, ResourceType resourceType, int resourceId)
{
    try 
    {
        // First verify the resource exists in the proper table
        bool resourceExists = false;
        
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

        // Always clear the tracker before operating on potentially tracked entities
        _context.ChangeTracker.Clear();
        
        // Use a SQL update approach instead of EF Core tracking to avoid conflicts
        try
        {
            // Get database connection string from context
            var connection = _context.Database.GetConnectionString();
            
            using (var conn = new NpgsqlConnection(connection))
            {
                await conn.OpenAsync();
                
                // Create a simple update command that doesn't rely on EF Core
                string updateSql = @"
                    UPDATE ""ConstructPlanItemDetails"" 
                    SET ""ResourceType"" = @ResourceType, 
                        ""ResourceId"" = @ResourceId,
                        ""UpdatedAt"" = @UpdatedAt
                    WHERE ""Id"" = @DetailId AND ""Deleted"" = false";
                
                using (var cmd = new NpgsqlCommand(updateSql, conn))
                {
                    cmd.Parameters.AddWithValue("ResourceType", (int)resourceType);
                    cmd.Parameters.AddWithValue("ResourceId", resourceId);
                    cmd.Parameters.AddWithValue("UpdatedAt", DateTime.UtcNow);
                    cmd.Parameters.AddWithValue("DetailId", detailId);
                    
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }
        catch (Exception ex)
        {
            // Log error but continue with the EF approach as fallback
            System.Diagnostics.Debug.WriteLine($"SQL update approach failed: {ex.Message}");
            
            // Fallback to EF Core approach
            _context.ChangeTracker.Clear();
            
            var detail = await _context.ConstructPlanItemDetails
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == detailId && !d.Deleted);
                
            if (detail != null)
            {
                // Create a new instance to avoid tracking conflicts
                var detailForUpdate = new ConstructPlanItemDetail
                {
                    Id = detail.Id,
                    PlanItemId = int.Parse(detail.PlanItemId.ToString()),
                    WorkCode = detail.WorkCode,
                    ResourceType = resourceType,
                    ResourceId = resourceId,
                    Quantity = detail.Quantity,
                    Unit = detail.Unit,
                    UnitPrice = detail.UnitPrice,
                    Total = detail.Total,
                    Creator = detail.Creator,
                    CreatedAt = detail.CreatedAt,
                    Updater = detail.Updater,
                    UpdatedAt = DateTime.UtcNow,
                    Deleted = false
                };
                
                _context.ConstructPlanItemDetails.Update(detailForUpdate);
                await _context.SaveChangesAsync();
                _context.Entry(detailForUpdate).State = EntityState.Detached;
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
            // Check if user is authorized to view construction plans
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

            // Verify if user is part of the project (except for Executive Board who can access all)
            if (!_helperService.IsInRole(actionBy, RoleConstValue.EXECUTIVE_BOARD))
            {
                var isUserInProject = await _context.ProjectUsers
                    .AnyAsync(pu => pu.ProjectId == constructionPlan.ProjectId && pu.UserId == actionBy && !pu.Deleted);
                if (!isUserInProject)
                {
                    throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED_PROJECT);
                }
            }

            // Get creator
            var creator = await _context.Users.FirstOrDefaultAsync(u => u.Id == constructionPlan.Creator);

            // Create DTO
            var dto = new ConstructionPlanDTO
            {
                Id = constructionPlan.Id,
                PlanName = constructionPlan.PlanName,
                ProjectId = constructionPlan.ProjectId,
                ProjectName = constructionPlan.Project?.ProjectName ?? "",
                CreatedAt = constructionPlan.CreatedAt ?? DateTime.UtcNow,
                UpdatedAt = constructionPlan.UpdatedAt ?? DateTime.UtcNow,
                CreatedBy = constructionPlan.Creator,
                CreatedByName = creator?.FullName ?? "",
                UpdatedBy = constructionPlan.Updater,
                // A plan is fully approved when all reviewers have approved (status true)
                IsApproved = constructionPlan.Reviewer != null && 
                            constructionPlan.Reviewer.Count > 0 && 
                            constructionPlan.Reviewer.All(r => r.Value == true)
            };

            // Add reviewers
            if (constructionPlan.Reviewers != null && constructionPlan.Reviewers.Any())
            {
                foreach (var reviewer in constructionPlan.Reviewers)
                {
                    bool? isApproved = null;
                    if (constructionPlan.Reviewer != null && constructionPlan.Reviewer.ContainsKey(reviewer.Id))
                    {
                        isApproved = constructionPlan.Reviewer[reviewer.Id];
                    }

                    dto.Reviewers.Add(new ReviewerDTO
                    {
                        Id = reviewer.Id,
                        Name = reviewer.FullName,
                        Email = reviewer.Email,
                        IsApproved = isApproved,
                        Role = reviewer.Role
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
                    Id = planItem.Id,
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
                    .Where(d => d.PlanItemId == planItem.Id && !d.Deleted)
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
                                        Type = 1
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
                                        Type = 2
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
                                        Type = 3
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
            
            // Verify if user is part of the project
            var isUserInProject = await _context.ProjectUsers
                .AnyAsync(pu => pu.ProjectId == constructionPlan.ProjectId && pu.UserId == actionBy && !pu.Deleted);
            if (!isUserInProject)
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED_PROJECT);
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
            
            // Clear project-specific caches to ensure project-related data is refreshed
            await _cacheService.DeleteAsync(RedisCacheKey.PROJECT_CACHE_KEY);
            await _cacheService.DeleteAsync(RedisCacheKey.PROJECT_LIST_CACHE_KEY);
            await _cacheService.DeleteAsync(string.Format(RedisCacheKey.PROJECT_BY_ID_CACHE_KEY, projectId));
            
            // Clear specific plan caches
            string planSpecificCacheKey = $"CONSTRUCTION_PLAN:ID:{planId}";
            await _cacheService.DeleteAsync(planSpecificCacheKey);
            
            // Clear project-specific construction plan caches
            string projectPlanCacheKey = $"CONSTRUCTION_PLAN:PROJECT:{projectId}";
            await _cacheService.DeleteAsync(projectPlanCacheKey);
            
            // Clear construction team caches as they might be associated with plans
            await _cacheService.DeleteAsync(RedisCacheKey.CONSTRUCTION_TEAM_CACHE_KEY);
            
            // Clear pattern-based caches using a pattern similar to ProjectService
            await _cacheService.DeleteByPatternAsync("CONSTRUCTION_PLAN:*");
            await _cacheService.DeleteByPatternAsync(RedisCacheKey.PROJECT_ALL_PATTERN);
        }

        public async Task<bool> Approve(ApproveConstructionPlanDTO model, int actionBy)
        {
            // Check if user is authorized to perform this action
            if (!_helperService.IsInRole(actionBy, new List<string> 
            { 
                RoleConstValue.TECHNICAL_MANAGER, 
                RoleConstValue.RESOURCE_MANAGER,
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

            // Verify if Technical/Resource manager is part of the project
            if (!_helperService.IsInRole(actionBy, RoleConstValue.EXECUTIVE_BOARD))
            {
                var isUserInProject = await _context.ProjectUsers
                    .AnyAsync(pu => pu.ProjectId == constructionPlan.ProjectId && pu.UserId == actionBy && !pu.Deleted);
                if (!isUserInProject)
                {
                    throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED_PROJECT);
                }
            }

            // Check if user is a reviewer
            if (!constructionPlan.Reviewers.Any(r => r.Id == actionBy))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Initialize reviewer dictionary if needed
            if (constructionPlan.Reviewer == null)
            {
                constructionPlan.Reviewer = new Dictionary<int, bool?>();
            }

            // Get user's role
            var userRole = constructionPlan.Reviewers.FirstOrDefault(r => r.Id == actionBy)?.Role;

            // Implement approval logic based on role
            if (userRole == RoleConstValue.RESOURCE_MANAGER)
            {
                // Case 1: Resource manager approves - only change their own status
                constructionPlan.Reviewer[actionBy] = true;
            }
            else if (userRole == RoleConstValue.TECHNICAL_MANAGER)
            {
                // Case 2: Technical manager approves - change their status and Resource Manager's
                constructionPlan.Reviewer[actionBy] = true;
                
                // Find and update Resource Manager's status
                var resourceManager = constructionPlan.Reviewers
                    .FirstOrDefault(r => r.Role == RoleConstValue.RESOURCE_MANAGER);
                    
                if (resourceManager != null)
                {
                    constructionPlan.Reviewer[resourceManager.Id] = true;
                }
            }
            else if (userRole == RoleConstValue.EXECUTIVE_BOARD)
            {
                // Case 3: Executive Board approves - change all statuses to true
                constructionPlan.Reviewer[actionBy] = true;
                
                // Find and update Technical Manager's status
                var technicalManager = constructionPlan.Reviewers
                    .FirstOrDefault(r => r.Role == RoleConstValue.TECHNICAL_MANAGER);
                    
                if (technicalManager != null)
                {
                    constructionPlan.Reviewer[technicalManager.Id] = true;
                }
                
                // Find and update Resource Manager's status
                var resourceManager = constructionPlan.Reviewers
                    .FirstOrDefault(r => r.Role == RoleConstValue.RESOURCE_MANAGER);
                    
                if (resourceManager != null)
                {
                    constructionPlan.Reviewer[resourceManager.Id] = true;
                }
            }

            constructionPlan.Updater = actionBy;

            _context.ConstructionPlans.Update(constructionPlan);
            await _context.SaveChangesAsync();

            // Invalidate all related caches
            await InvalidateConstructionPlanCaches(constructionPlan.Id, constructionPlan.ProjectId);
            
            return true;
        }

        public async Task<bool> Reject(ApproveConstructionPlanDTO model, int actionBy)
        {
            // Only Executive Board can reject construction plans
            if (!_helperService.IsInRole(actionBy, RoleConstValue.EXECUTIVE_BOARD))
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
            if (!constructionPlan.Reviewers.Any(r => r.Id == actionBy && r.Role == RoleConstValue.EXECUTIVE_BOARD))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Initialize reviewer dictionary if needed
            if (constructionPlan.Reviewer == null)
            {
                constructionPlan.Reviewer = new Dictionary<int, bool?>();
            }

            // Case 4: Executive Board rejects - mark their status as false, reset others to null
            constructionPlan.Reviewer[actionBy] = false;
            
            // Reset Technical and Resource Manager statuses to null
            foreach (var reviewer in constructionPlan.Reviewers)
            {
                if (reviewer.Role == RoleConstValue.TECHNICAL_MANAGER || 
                    reviewer.Role == RoleConstValue.RESOURCE_MANAGER)
                {
                    constructionPlan.Reviewer[reviewer.Id] = null;
                }
            }

            constructionPlan.Updater = actionBy;

            _context.ConstructionPlans.Update(constructionPlan);
            await _context.SaveChangesAsync();

            // Invalidate all related caches
            await InvalidateConstructionPlanCaches(constructionPlan.Id, constructionPlan.ProjectId);
            
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
            
            // Verify if user is part of the project
            var isUserInProject = await _context.ProjectUsers
                .AnyAsync(pu => pu.ProjectId == constructionPlan.ProjectId && pu.UserId == actionBy && !pu.Deleted);
            if (!isUserInProject)
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED_PROJECT);
            }

            // Get plan item - modify to search by Id instead of WorkCode
            // Update this part once we update the DTO to include item id
            var planItem = await _context.ConstructPlanItems
                .Include(pi => pi.ConstructionTeams)
                .Include(pi => pi.ConstructionPlan) 
                .FirstOrDefaultAsync(pi => pi.PlanId == model.PlanId && 
                                           ((model.Id.HasValue && pi.Id == model.Id) || 
                                            (!model.Id.HasValue && pi.WorkCode == model.WorkCode)) && 
                                           !pi.Deleted);

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

            // Invalidate all related caches
            await InvalidateConstructionPlanCaches(model.PlanId, constructionPlan.ProjectId);
            
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
            
            // Verify if user is part of the project
            var isUserInProject = await _context.ProjectUsers
                .AnyAsync(pu => pu.ProjectId == model.ProjectId && pu.UserId == actionBy && !pu.Deleted);
            if (!isUserInProject)
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED_PROJECT);
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
                Reviewer = new Dictionary<int, bool?>() // Changed to nullable bool
            };

            // Initialize reviewers collection
            constructionPlan.Reviewers = new List<User>();

            // Find the Technical Manager for this project
            var technicalManager = await _context.ProjectUsers
                .Include(pu => pu.User)
                .Where(pu => pu.ProjectId == model.ProjectId 
                      && !pu.Deleted 
                      && pu.User.Role == RoleConstValue.TECHNICAL_MANAGER 
                      && !pu.User.Deleted)
                .Select(pu => pu.User)
                .FirstOrDefaultAsync();

            // Find the Resource Manager for this project
            var resourceManager = await _context.ProjectUsers
                .Include(pu => pu.User)
                .Where(pu => pu.ProjectId == model.ProjectId 
                      && !pu.Deleted 
                      && pu.User.Role == RoleConstValue.RESOURCE_MANAGER 
                      && !pu.User.Deleted)
                .Select(pu => pu.User)
                .FirstOrDefaultAsync();

            // Find the Executive Board member
            var executiveBoard = await _context.Users
                .Where(u => u.Role == RoleConstValue.EXECUTIVE_BOARD && !u.Deleted)
                .FirstOrDefaultAsync();

            // Add the Technical Manager as reviewer if found
            if (technicalManager != null)
            {
                constructionPlan.Reviewers.Add(technicalManager);
                constructionPlan.Reviewer.Add(technicalManager.Id, null); // Initialize as null
            }
            
            // Add the Resource Manager as reviewer if found
            if (resourceManager != null && (technicalManager == null || technicalManager.Id != resourceManager.Id))
            {
                constructionPlan.Reviewers.Add(resourceManager);
                constructionPlan.Reviewer.Add(resourceManager.Id, null); // Initialize as null
            }

            // Add the Executive Board member as reviewer if found
            if (executiveBoard != null && 
                (technicalManager == null || technicalManager.Id != executiveBoard.Id) &&
                (resourceManager == null || resourceManager.Id != executiveBoard.Id))
            {
                constructionPlan.Reviewers.Add(executiveBoard);
                constructionPlan.Reviewer.Add(executiveBoard.Id, null); // Initialize as null
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
                
                // After successful import and saving to database, invalidate all caches
                await InvalidateConstructionPlanCaches(constructionPlan.Id, constructionPlan.ProjectId);

                // Return the result
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