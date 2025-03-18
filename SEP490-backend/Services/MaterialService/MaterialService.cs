using Microsoft.EntityFrameworkCore;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Infra.Helps;
using Sep490_Backend.Services.HelperService;
using Sep490_Backend.Services.DataService;
using Sep490_Backend.Services.CacheService;
using Sep490_Backend.DTO.Common;
using System.Text.RegularExpressions;
using Sep490_Backend.Controllers;

namespace Sep490_Backend.Services.MaterialService
{
    public interface IMaterialService
    {
        Task<Material> SaveMaterial(Material model, int actionBy);
        Task<bool> DeleteMaterial(int materialId, int actionBy);
    }

    public class MaterialService : IMaterialService
    {
        private readonly BackendContext _context;
        private readonly IHelperService _helperService;
        private readonly ICacheService _cacheService;
        private readonly IDataService _dataService;

        public MaterialService(
            BackendContext context, 
            IHelperService helperService, 
            ICacheService cacheService,
            IDataService dataService)
        {
            _context = context;
            _helperService = helperService;
            _cacheService = cacheService;
            _dataService = dataService;
        }

        /// <summary>
        /// Creates or updates a material
        /// </summary>
        /// <param name="model">Material model with data to save</param>
        /// <param name="actionBy">ID of the user performing the action</param>
        /// <returns>The saved material entity</returns>
        public async Task<Material> SaveMaterial(Material model, int actionBy)
        {
            var errors = new List<ResponseError>();

            // Authorization check - only Resource Manager can manage materials
            if (!_helperService.IsInRole(actionBy, RoleConstValue.RESOURCE_MANAGER))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Validation
            if (string.IsNullOrWhiteSpace(model.MaterialCode))
            {
                errors.Add(new ResponseError
                {
                    Message = Message.CommonMessage.MISSING_PARAM,
                    Field = nameof(model.MaterialCode).ToCamelCase()
                });
            }

            if (string.IsNullOrWhiteSpace(model.MaterialName))
            {
                errors.Add(new ResponseError
                {
                    Message = Message.CommonMessage.MISSING_PARAM,
                    Field = nameof(model.MaterialName).ToCamelCase()
                });
            }

            // Check for duplicate MaterialCode
            var existingMaterial = await _context.Materials
                .FirstOrDefaultAsync(m => m.MaterialCode == model.MaterialCode && m.Id != model.Id && !m.Deleted);

            if (existingMaterial != null)
            {
                errors.Add(new ResponseError
                {
                    Message = "Material code already exists",
                    Field = nameof(model.MaterialCode).ToCamelCase()
                });
            }

            // Throw aggregated errors
            if (errors.Count > 0)
                throw new ValidationException(errors);

            // If ID is provided, update existing material
            if (model.Id > 0)
            {
                var materialToUpdate = await _context.Materials
                    .FirstOrDefaultAsync(m => m.Id == model.Id && !m.Deleted);

                if (materialToUpdate == null)
                {
                    throw new KeyNotFoundException("Material not found");
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
                materialToUpdate.Inventory = model.Inventory;
                materialToUpdate.Attachment = model.Attachment;
                materialToUpdate.ExpireDate = model.ExpireDate;
                materialToUpdate.ProductionDate = model.ProductionDate;
                materialToUpdate.Description = model.Description;
                
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
            // Authorization check - only Resource Manager can delete materials
            if (!_helperService.IsInRole(actionBy, RoleConstValue.RESOURCE_MANAGER))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Find the material with its construction plan items
            var material = await _context.Materials
                .Include(m => m.ConstructPlanItemDetails)
                .FirstOrDefaultAsync(m => m.Id == materialId && !m.Deleted);

            if (material == null)
            {
                throw new KeyNotFoundException("Material not found");
            }

            // Check if the material is in use in construction plans
            if (material.ConstructPlanItemDetails != null && material.ConstructPlanItemDetails.Any())
            {
                throw new InvalidOperationException("Cannot delete material because it is in use in construction plans");
            }

            // Perform soft delete
            material.Deleted = true;
            material.UpdatedAt = DateTime.Now;
            material.Updater = actionBy;

            _context.Materials.Update(material);
            await _context.SaveChangesAsync();

            // Invalidate cache
            await InvalidateMaterialCache();

            return true;
        }

        /// <summary>
        /// Invalidates material-related cache
        /// </summary>
        private async Task InvalidateMaterialCache()
        {
            await _cacheService.DeleteAsync(RedisCacheKey.MATERIAL_CACHE_KEY);
        }
    }
}
