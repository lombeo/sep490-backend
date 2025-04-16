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
        private readonly ICacheService _cacheService;

        public VehicleController(IVehicleService vehicleService, ICacheService cacheService)
        {
            _vehicleService = vehicleService;
            _cacheService = cacheService;
        }

        [HttpGet("{id}")]
        [ResponseCache(Duration = 60, VaryByQueryKeys = new[] { "id" })]
        public async Task<ResponseDTO<Vehicle>> GetById(int id)
        {
            var result = await HandleException(_vehicleService.GetVehicleById(id), Message.VehicleMessage.GET_DETAIL_SUCCESS);
            return result;
        }

        [HttpGet("search")]
        [ResponseCache(Duration = 30, VaryByQueryKeys = new[] { "LicensePlate", "Brand", "VehicleType", "Status", "Driver", "PageIndex", "PageSize" })]
        public async Task<ResponseDTO<List<Vehicle>>> Search([FromQuery] VehicleSearchDTO model)
        {
            model.ActionBy = UserId;
            var result = await HandleException(_vehicleService.GetVehicles(model), Message.VehicleMessage.SEARCH_SUCCESS);
            result.Meta = new ResponseMeta() { Total = model.Total, Index = model.PageIndex, PageSize = model.PageSize };
            return result;
        }

        [HttpPost("create")]
        [ResponseCache(NoStore = true)]
        public async Task<ResponseDTO<Vehicle>> Create([FromForm] VehicleCreateDTO model)
        {
            var result = await HandleException(_vehicleService.CreateVehicle(model, UserId), Message.VehicleMessage.CREATE_SUCCESS);
            return result;
        }

        [HttpPut("update")]
        [ResponseCache(NoStore = true)]
        public async Task<ResponseDTO<Vehicle>> Update([FromForm] VehicleUpdateDTO model)
        {
            var result = await HandleException(_vehicleService.UpdateVehicle(model, UserId), Message.VehicleMessage.UPDATE_SUCCESS);
            return result;
        }

        [HttpDelete("delete/{id}")]
        [ResponseCache(NoStore = true)]
        public async Task<ResponseDTO<bool>> Delete(int id)
        {
            var result = await HandleException(_vehicleService.DeleteVehicle(id, UserId), Message.VehicleMessage.DELETE_SUCCESS);
            return result;
        }
    }
} 