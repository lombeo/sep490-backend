﻿using Microsoft.EntityFrameworkCore;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Infra.Helps;
using Sep490_Backend.Infra.Enums;
using Sep490_Backend.Services.HelperService;
using Sep490_Backend.Services.DataService;
using Sep490_Backend.Services.CacheService;
using Sep490_Backend.DTO.Common;
using System.Text.RegularExpressions;
using Sep490_Backend.Controllers;
using Sep490_Backend.DTO.Material;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore.Storage;

namespace Sep490_Backend.Services.MaterialService
{
    public interface IMaterialService
    {
        Task<Material> SaveMaterial(MaterialSaveDTO model, int actionBy);
        Task<bool> DeleteMaterial(int materialId, int actionBy);
        Task<MaterialDetailDTO> GetMaterialById(int materialId, int actionBy);
        Task<int> RollbackMaterialsFromProjectInventory(int projectId, int actionBy, IDbContextTransaction externalTransaction = null);
    }

    public class MaterialService : IMaterialService
    {
        private readonly BackendContext _context;
        private readonly IHelperService _helperService;
        private readonly ICacheService _cacheService;
        private readonly IDataService _dataService;
        private readonly ILogger<MaterialService> _logger;
        private readonly TimeSpan DEFAULT_CACHE_DURATION = TimeSpan.FromMinutes(15);

        public MaterialService(
            BackendContext context, 
            IHelperService helperService, 
            ICacheService cacheService,
            IDataService dataService,
            ILogger<MaterialService> logger)
        {
            _context = context;
            _helperService = helperService;
            _cacheService = cacheService;
            _dataService = dataService;
            _logger = logger;
        }

        /// <summary>
        /// Creates or updates a material
        /// </summary>
        /// <param name="model">MaterialSaveDTO model with data to save</param>
        /// <param name="actionBy">ID of the user performing the action</param>
        /// <returns>The saved material entity</returns>
        public async Task<Material> SaveMaterial(MaterialSaveDTO model, int actionBy)
        {
            var errors = new List<ResponseError>();

            // Authorization check - only Administrator can manage materials
            if (!_helperService.IsInRole(actionBy, RoleConstValue.ADMIN))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Validation
            if (string.IsNullOrWhiteSpace(model.MaterialCode))
            {
                errors.Add(new ResponseError
                {
                    Message = Message.MaterialMessage.CODE_REQUIRED,
                    Field = nameof(model.MaterialCode).ToCamelCase()
                });
            }

            if (string.IsNullOrWhiteSpace(model.MaterialName))
            {
                errors.Add(new ResponseError
                {
                    Message = Message.MaterialMessage.NAME_REQUIRED,
                    Field = nameof(model.MaterialName).ToCamelCase()
                });
            }

            // Check for duplicate MaterialCode
            var existingMaterial = await _context.Materials
                .FirstOrDefaultAsync(m => m.MaterialCode == model.MaterialCode && (model.Id == null || m.Id != model.Id) && !m.Deleted);

            if (existingMaterial != null)
            {
                errors.Add(new ResponseError
                {
                    Message = Message.MaterialMessage.CODE_EXISTS,
                    Field = nameof(model.MaterialCode).ToCamelCase()
                });
            }

            // Throw aggregated errors
            if (errors.Count > 0)
                throw new ValidationException(errors);

            // If ID is provided, update existing material
            if (model.Id.HasValue && model.Id.Value > 0)
            {
                var materialToUpdate = await _context.Materials
                    .FirstOrDefaultAsync(m => m.Id == model.Id.Value && !m.Deleted);

                if (materialToUpdate == null)
                {
                    throw new KeyNotFoundException(Message.MaterialMessage.NOT_FOUND);
                }

                // Update properties
                materialToUpdate.MaterialCode = model.MaterialCode;
                materialToUpdate.MaterialName = model.MaterialName;
                materialToUpdate.Unit = model.Unit;
                materialToUpdate.Branch = model.Branch;
                materialToUpdate.MadeIn = model.MadeIn;
                materialToUpdate.ChassisNumber = model.ChassisNumber;
                materialToUpdate.WholesalePrice = model.WholesalePrice;
                materialToUpdate.RetailPrice = model.RetailPrice;
                materialToUpdate.Inventory = model.Inventory ?? 0;
                materialToUpdate.Attachment = model.Attachment;
                materialToUpdate.ExpireDate = model.ExpireDate;
                materialToUpdate.ProductionDate = model.ProductionDate;
                materialToUpdate.Description = model.Description ?? "";
                materialToUpdate.CanRollBack = model.CanRollBack;
                
                // Update audit fields
                materialToUpdate.UpdatedAt = DateTime.Now;
                materialToUpdate.Updater = actionBy;

                _context.Materials.Update(materialToUpdate);
                await _context.SaveChangesAsync();

                // Invalidate cache if needed
                await InvalidateMaterialCache();

                return materialToUpdate;
            }
            else
            {
                // Create new material
                var newMaterial = new Material
                {
                    MaterialCode = model.MaterialCode,
                    MaterialName = model.MaterialName,
                    Unit = model.Unit,
                    Branch = model.Branch,
                    MadeIn = model.MadeIn,
                    ChassisNumber = model.ChassisNumber,
                    WholesalePrice = model.WholesalePrice,
                    RetailPrice = model.RetailPrice,
                    Inventory = model.Inventory ?? 0,
                    Attachment = model.Attachment,
                    ExpireDate = model.ExpireDate,
                    ProductionDate = model.ProductionDate,
                    Description = model.Description ?? "",
                    CanRollBack = model.CanRollBack,
                    
                    // Set audit fields
                    Creator = actionBy,
                    Updater = actionBy,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                await _context.Materials.AddAsync(newMaterial);
                await _context.SaveChangesAsync();

                // Invalidate cache if needed
                await InvalidateMaterialCache();

                return newMaterial;
            }
        }

        /// <summary>
        /// Deletes a material (soft delete)
        /// </summary>
        /// <param name="materialId">ID of the material to delete</param>
        /// <param name="actionBy">ID of the user performing the action</param>
        /// <returns>True if deletion was successful, otherwise false</returns>
        public async Task<bool> DeleteMaterial(int materialId, int actionBy)
        {
            // Authorization check - only Administrator can delete materials
            if (!_helperService.IsInRole(actionBy, RoleConstValue.ADMIN))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Find the material with its construction plan items
            var material = await _context.Materials
                .Include(m => m.ConstructPlanItemDetails)
                .FirstOrDefaultAsync(m => m.Id == materialId && !m.Deleted);

            if (material == null)
            {
                throw new KeyNotFoundException(Message.MaterialMessage.NOT_FOUND);
            }

            // Check if the material is in use in construction plans
            if (material.ConstructPlanItemDetails != null && material.ConstructPlanItemDetails.Any())
            {
                throw new InvalidOperationException(Message.MaterialMessage.MATERIAL_IN_USE);
            }

            // Use the extension method for soft delete
            await _context.SoftDeleteAsync(material, actionBy);

            // Invalidate cache
            await InvalidateMaterialCache();

            return true;
        }

        /// <summary>
        /// Invalidates material-related cache
        /// </summary>
        private async Task InvalidateMaterialCache()
        {
            // Clear the main materials cache
            await _cacheService.DeleteAsync(RedisCacheKey.MATERIAL_CACHE_KEY);
            
            // Delete specific material caches using pattern matching
            await _cacheService.DeleteByPatternAsync($"{RedisCacheKey.MATERIAL_CACHE_KEY}_ID_");
            
            // Delete material search caches with patterns (used by DataService.ListMaterial)
            await _cacheService.DeleteByPatternAsync($"{RedisCacheKey.MATERIAL_CACHE_KEY}_KEYWORD_");
            
            // Delete any inventory related caches that might have material info
            await _cacheService.DeleteByPatternAsync(RedisCacheKey.RESOURCE_INVENTORY_CACHE_KEY);
            await _cacheService.DeleteByPatternAsync(RedisCacheKey.RESOURCE_INVENTORY_BY_TYPE_CACHE_KEY);
            
            _logger.LogInformation("Material cache invalidated successfully");
        }

        public async Task<MaterialDetailDTO> GetMaterialById(int materialId, int actionBy)
        {
            // Remove role-based authorization check - all authenticated users can view material details

            // Create cache key for this specific material
            string cacheKey = $"{RedisCacheKey.MATERIAL_CACHE_KEY}_ID_{materialId}";
            
            // Try to get from cache first
            var cachedMaterial = await _cacheService.GetAsync<MaterialDetailDTO>(cacheKey);
            
            if (cachedMaterial != null)
            {
                _logger.LogInformation($"Cache hit for material detail: {cacheKey}");
                return cachedMaterial;
            }
            
            _logger.LogInformation($"Cache miss for material detail: {cacheKey}, fetching from database");

            // Find the material in the database
            var material = await _context.Materials
                .FirstOrDefaultAsync(m => m.Id == materialId && !m.Deleted);

            if (material == null)
            {
                throw new KeyNotFoundException(Message.MaterialMessage.NOT_FOUND);
            }

            // Map the entity to DTO
            var materialDetailDTO = new MaterialDetailDTO
            {
                Id = material.Id,
                MaterialCode = material.MaterialCode,
                MaterialName = material.MaterialName,
                Unit = material.Unit,
                Branch = material.Branch,
                MadeIn = material.MadeIn,
                ChassisNumber = material.ChassisNumber,
                WholesalePrice = material.WholesalePrice,
                RetailPrice = material.RetailPrice,
                Inventory = material.Inventory,
                Attachment = material.Attachment,
                ExpireDate = material.ExpireDate,
                ProductionDate = material.ProductionDate,
                Description = material.Description,
                CreatedAt = material.CreatedAt,
                UpdatedAt = material.UpdatedAt,
                Creator = material.Creator,
                Updater = material.Updater
            };

            // Cache the result
            await _cacheService.SetAsync(cacheKey, materialDetailDTO, DEFAULT_CACHE_DURATION);
            
            return materialDetailDTO;
        }

        /// <summary>
        /// Rolls back materials from a project's inventory to the main material inventory
        /// </summary>
        /// <param name="projectId">ID of the project</param>
        /// <param name="actionBy">ID of the user performing the action</param>
        /// <param name="externalTransaction">Optional external transaction to use instead of creating a new one</param>
        /// <returns>The number of materials rolled back</returns>
        public async Task<int> RollbackMaterialsFromProjectInventory(int projectId, int actionBy, IDbContextTransaction externalTransaction = null)
        {
            _logger.LogInformation($"Rolling back materials from project {projectId} to main inventory");
            
            // Track if we're managing the transaction or using an external one
            bool manageTransaction = externalTransaction == null;
            IDbContextTransaction transaction = externalTransaction;
            
            try
            {
                // Start transaction only if we're not using an external one
                if (manageTransaction)
                {
                    transaction = await _context.Database.BeginTransactionAsync();
                }
                
                // Get all materials in the project inventory
                var resourceInventories = await _context.ResourceInventory
                    .Where(ri => ri.ProjectId == projectId && 
                                ri.ResourceType == ResourceType.MATERIAL && 
                                ri.ResourceId.HasValue && 
                                !ri.Deleted)
                    .ToListAsync();
                
                if (!resourceInventories.Any())
                {
                    _logger.LogInformation($"No materials found in project {projectId} to roll back");
                    if (manageTransaction)
                    {
                        await transaction.CommitAsync();
                    }
                    return 0;
                }
                
                int rolledBackCount = 0;
                
                foreach (var inventory in resourceInventories)
                {
                    // Get the associated material
                    var material = await _context.Materials
                        .FirstOrDefaultAsync(m => m.Id == inventory.ResourceId.Value && !m.Deleted);
                    
                    if (material == null)
                    {
                        _logger.LogWarning($"Material with ID {inventory.ResourceId.Value} not found or deleted, skipping rollback");
                        continue;
                    }
                    
                    // Roll back all materials regardless of CanRollBack flag when project is completed
                    // Update material inventory
                    material.Inventory = (material.Inventory ?? 0) + inventory.Quantity;
                    material.UpdatedAt = DateTime.Now;
                    material.Updater = actionBy;
                    
                    _context.Materials.Update(material);
                    
                    // Soft delete the resource inventory entry
                    inventory.Deleted = true;
                    inventory.UpdatedAt = DateTime.Now;
                    inventory.Updater = actionBy;
                    
                    _context.ResourceInventory.Update(inventory);
                    
                    rolledBackCount++;
                    
                    _logger.LogInformation($"Rolled back material {material.Id} ({material.MaterialName}), inventory increased from {material.Inventory - inventory.Quantity} to {material.Inventory}");
                }
                
                await _context.SaveChangesAsync();
                
                // Only commit if we're managing the transaction
                if (manageTransaction)
                {
                    await transaction.CommitAsync();
                }
                
                // Invalidate caches only if we actually rolled back materials
                if (rolledBackCount > 0)
                {
                    await InvalidateMaterialCache();
                    await _cacheService.DeleteByPatternAsync(RedisCacheKey.RESOURCE_INVENTORY_CACHE_KEY);
                    await _cacheService.DeleteByPatternAsync(RedisCacheKey.RESOURCE_INVENTORY_BY_TYPE_CACHE_KEY);
                    // Also invalidate project-specific resource caches
                    // These patterns will catch any project-specific resource inventory caches
                    // that might be using project ID as part of the key
                    await _cacheService.DeleteByPatternAsync($"*PROJECT:{projectId}*");
                    await _cacheService.DeleteByPatternAsync($"*:PROJECT:{projectId}*");
                    
                    // Invalidate project status cache since resources affect project status
                    await _cacheService.DeleteAsync(RedisCacheKey.PROJECT_STATUS_CACHE_KEY);
                }
                
                _logger.LogInformation($"Successfully rolled back {rolledBackCount} materials from project {projectId}");
                return rolledBackCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error rolling back materials from project {projectId}: {ex.Message}");
                
                // Only rollback if we're managing the transaction
                if (manageTransaction && transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                
                throw;
            }
        }
    }
}
