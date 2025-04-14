using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.Vehicle;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Services.VehicleService;
using Sep490_Backend.Services.CacheService;

namespace Sep490_Backend.Controllers
{
    [ApiController]
    [Route(RouteApiConstant.BASE_PATH + "/vehicle")]
    [Authorize]
    public class VehicleController : BaseAPIController
    {
        private readonly IVehicleService _vehicleService;
        private readonly ILogger<VehicleController> _logger;
        private readonly ICacheService _cacheService;

        public VehicleController(IVehicleService vehicleService, ILogger<VehicleController> logger, ICacheService cacheService)
        {
            _vehicleService = vehicleService;
            _logger = logger;
            _cacheService = cacheService;
        }

        [HttpGet("{id}")]
        [ResponseCache(Duration = 60, VaryByQueryKeys = new[] { "id" })]
        public async Task<ResponseDTO<Vehicle>> GetById(int id)
        {
            _logger.LogInformation($"Getting vehicle with ID: {id}");
            var result = await HandleException(_vehicleService.GetVehicleById(id), Message.VehicleMessage.GET_DETAIL_SUCCESS);
            return result;
        }

        [HttpGet("search")]
        [ResponseCache(Duration = 30, VaryByQueryKeys = new[] { "LicensePlate", "Brand", "VehicleType", "Status", "Driver", "PageIndex", "PageSize" })]
        public async Task<ResponseDTO<List<Vehicle>>> Search([FromQuery] VehicleSearchDTO model)
        {
            _logger.LogInformation($"Searching vehicles with filters: LicensePlate={model.LicensePlate}, Brand={model.Brand}, Type={model.VehicleType}, Status={model.Status}, Driver={model.Driver}");
            model.ActionBy = UserId;
            var result = await HandleException(_vehicleService.GetVehicles(model), Message.VehicleMessage.SEARCH_SUCCESS);
            result.Meta = new ResponseMeta() { Total = model.Total, Index = model.PageIndex, PageSize = model.PageSize };
            return result;
        }

        [HttpPost("create")]
        [ResponseCache(NoStore = true)]
        public async Task<ResponseDTO<Vehicle>> Create([FromForm] VehicleCreateDTO model)
        {
            _logger.LogInformation($"Creating new vehicle with license plate: {model.LicensePlate}");
            var result = await HandleException(_vehicleService.CreateVehicle(model, UserId), Message.VehicleMessage.CREATE_SUCCESS);
            return result;
        }

        [HttpPut("update")]
        [ResponseCache(NoStore = true)]
        public async Task<ResponseDTO<Vehicle>> Update([FromForm] VehicleUpdateDTO model)
        {
            _logger.LogInformation($"Updating vehicle with ID: {model.Id}");
            var result = await HandleException(_vehicleService.UpdateVehicle(model, UserId), Message.VehicleMessage.UPDATE_SUCCESS);
            return result;
        }

        [HttpDelete("delete/{id}")]
        [ResponseCache(NoStore = true)]
        public async Task<ResponseDTO<bool>> Delete(int id)
        {
            _logger.LogInformation($"Deleting vehicle with ID: {id}");
            var result = await HandleException(_vehicleService.DeleteVehicle(id, UserId), Message.VehicleMessage.DELETE_SUCCESS);
            return result;
        }

        /// <summary>
        /// Manually invalidates vehicle caches to ensure fresh data is fetched
        /// </summary>
        /// <returns>Success message</returns>
        [HttpPost("clear-cache")]
        [ResponseCache(NoStore = true)]
        public async Task<ResponseDTO<string>> ClearVehicleCache()
        {
            try
            {
                // Clear all vehicle-related caches using pattern deletion
                await _cacheService.DeleteByPatternAsync(RedisCacheKey.VEHICLE_CACHE_KEY);
                
                return new ResponseDTO<string>
                {
                    Code = (int)RESPONSE_CODE.OK,
                    Message = Message.CommonMessage.ACTION_SUCCESS,
                    Data = "All vehicle caches cleared. Reload your page to get fresh data."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing vehicle cache");
                return new ResponseDTO<string>
                {
                    Code = (int)RESPONSE_CODE.InternalServerError,
                    Message = ex.Message,
                    Data = null
                };
            }
        }
    }
} 