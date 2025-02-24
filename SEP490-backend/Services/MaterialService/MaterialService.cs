using Microsoft.EntityFrameworkCore;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.Material;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Infra.Helps;
using Sep490_Backend.Services.AuthenService;
using Sep490_Backend.Services.CacheService;
using Sep490_Backend.Services.DataService;
using Sep490_Backend.Services.HelperService;

namespace Sep490_Backend.Services.MaterialService
{

    public interface IMaterialService
    {
        Task<Material> GetDetailMaterial(int materialId, int actionBy);
        Task<int> DeleteMaterial(int materialId, int actionBy);
        Task<Material> CreateMaterial(MaterialCreateDTO model, int actionBy);
        Task<Material> UpdateMaterial(Material model, int actionBy);
    }
    public class MaterialService : IMaterialService
    {
        private readonly BackendContext _context;
        private readonly IAuthenService _authenService;
        private readonly IHelperService _helperService;
        private readonly ICacheService _cacheService;
        private readonly IDataService _dataService;
        public MaterialService(BackendContext context, IAuthenService authenService, IHelperService helperService, ICacheService cacheService, IDataService dataService)
        {
            _context = context;
            _authenService = authenService;
            _helperService = helperService;
            _cacheService = cacheService;
            _dataService = dataService;
        }

        public async Task<Material> GetDetailMaterial(int materialId, int actionBy)
        {
            if (!_helperService.IsInRole(actionBy, new List<string> { RoleConstValue.RESOURCE_MANAGER, RoleConstValue.EXECUTIVE_BOARD }))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            var data = await _dataService.ListMaterial(new MaterialSearchDTO()
            {
                ActionBy = actionBy,
                PageSize = int.MaxValue
            });
            var result = data.FirstOrDefault(t => t.Id == materialId);
            if (result == null)
            {
                throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
            }
            return result;
        }

        public async Task<int> DeleteMaterial(int materialId, int actionBy)
        {
            if (!_helperService.IsInRole(actionBy, RoleConstValue.RESOURCE_MANAGER))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            var data = await _context.Materials.FirstOrDefaultAsync(t => t.Id == materialId);
            if (data == null)
            {
                throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
            }
            data.Deleted = true;
            _context.Update(data);
            await _context.SaveChangesAsync();

            _ = _cacheService.DeleteAsync(RedisCacheKey.MATERIAL_CACHE_KEY);

            return data.Id;
        }

        public async Task<Material> CreateMaterial(MaterialCreateDTO model, int actionBy)
        {
            var errors = new List<ResponseError>();
            if (!_helperService.IsInRole(actionBy, new List<string> { RoleConstValue.RESOURCE_MANAGER }))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            if(string.IsNullOrWhiteSpace(model.MaterialCode))
                errors.Add(new ResponseError
                {
                    Message = Message.CommonMessage.MISSING_PARAM,
                    Field = nameof(model.MaterialCode).ToCamelCase()
                });

            if (string.IsNullOrWhiteSpace(model.MaterialName))
                errors.Add(new ResponseError
                {
                    Message = Message.CommonMessage.MISSING_PARAM,
                    Field = nameof(model.MaterialName).ToCamelCase()
                });

            var data = await _dataService.ListMaterial(new MaterialSearchDTO() { ActionBy = actionBy, PageSize = int.MaxValue });
            if (data.FirstOrDefault(t => t.MaterialCode == model.MaterialCode) != null)
            {
                errors.Add(new ResponseError
                {
                    Message = Message.MaterialMessage.MATERIAL_CODE_DUPLICATE,
                    Field = nameof(model.MaterialCode).ToCamelCase()
                });
            }

            if (errors.Count > 0)
                throw new ValidationException(errors);

            var material = new Material
            {
                MaterialCode = model.MaterialCode,
                MaterialName = model.MaterialName ?? "",
                Unit = model.Unit ?? "",
                Branch = model.Branch ?? "",
                MadeIn = model.MadeIn ?? "",
                ChassisNumber = model.ChassisNumber ?? "",
                RetailPrice = model.RetailPrice == null ? model.RetailPrice : 0m,
                WholesalePrice = model.WholesalePrice == null ? model.WholesalePrice : 0m,
                Inventory = model.Inventory == null ? model.Inventory : 0,
                Attachment = model.Attachment ?? "",
                ExpireDate = model.ExpireDate ?? DateTime.UtcNow,  
                ProductionDate = model.ProductionDate ?? DateTime.UtcNow,
                Description = model.Description ?? "",
                CreatedAt = DateTime.UtcNow,

                Updater = actionBy,
                Creator = actionBy,
            };

            await _context.AddAsync(material);
            await _context.SaveChangesAsync();
            _ = _cacheService.DeleteAsync(RedisCacheKey.MATERIAL_CACHE_KEY);

            return material;

        }

        public async Task<Material> UpdateMaterial(Material model, int actionBy)
        {
            var errors = new List<ResponseError>();
            if (!_helperService.IsInRole(actionBy, RoleConstValue.RESOURCE_MANAGER))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            var existMaterial = await _context.Materials.FirstOrDefaultAsync(c => c.Id == model.Id);
            if (existMaterial == null)
            {
                throw new KeyNotFoundException(Message.MaterialMessage.MATERIAL_NOT_FOUND);
            }

            if (string.IsNullOrWhiteSpace(model.MaterialCode))
                errors.Add(new ResponseError
                {
                    Message = Message.CommonMessage.MISSING_PARAM,
                    Field = nameof(model.MaterialCode).ToCamelCase()
                });


            var data = await _dataService.ListMaterial(new MaterialSearchDTO() { ActionBy = actionBy, PageSize = int.MaxValue });
            if (data.FirstOrDefault(t => t.MaterialCode == model.MaterialCode) != null)
            {
                errors.Add(new ResponseError
                {
                    Message = Message.MaterialMessage.MATERIAL_CODE_DUPLICATE,
                    Field = nameof(model.MaterialCode).ToCamelCase()
                });
            }

            // Throw aggregated errors
            if (errors.Count > 0)
                throw new ValidationException(errors);

            existMaterial.MaterialCode = model.MaterialCode ?? existMaterial.MaterialCode;
            existMaterial.MaterialName = model.MaterialName ?? existMaterial.MaterialName;
            existMaterial.Unit = model.Unit ?? existMaterial.Unit;
            existMaterial.Branch = model.Branch ?? existMaterial.Branch;
            existMaterial.MadeIn = model.MadeIn ?? existMaterial.MadeIn;
            existMaterial.ChassisNumber = model.ChassisNumber ?? existMaterial.ChassisNumber;
            existMaterial.RetailPrice = model.RetailPrice == null ? model.RetailPrice : existMaterial.RetailPrice;
            existMaterial.WholesalePrice = model.WholesalePrice == null ? model.WholesalePrice : existMaterial.WholesalePrice;
            existMaterial.Inventory = model.Inventory == null ? model.Inventory : existMaterial.Inventory;
            existMaterial.Attachment = model.Attachment ?? existMaterial.Attachment;
            existMaterial.ExpireDate = model.ExpireDate ?? existMaterial.ExpireDate;
            existMaterial.ProductionDate = model.ProductionDate ?? existMaterial.ProductionDate;
            existMaterial.Description = model.Description ?? existMaterial.Description;

            existMaterial.CreatedAt = DateTime.UtcNow;
            existMaterial.Updater = actionBy;

            _context.Update(existMaterial);
            _context.SaveChanges();
            _ = _cacheService.DeleteAsync(RedisCacheKey.MATERIAL_CACHE_KEY);
            return existMaterial;
        }

        public class ValidationException : Exception
        {
            public List<ResponseError> Errors { get; }

            public ValidationException(List<ResponseError> errors)
                : base("One or more validation errors occurred.")
            {
                Errors = errors;
            }
        }
    }
}
