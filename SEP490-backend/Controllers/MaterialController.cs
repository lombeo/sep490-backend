using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.Material;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Services.DataService;
using Sep490_Backend.Services.MaterialService;

namespace Sep490_Backend.Controllers
{
    [ApiController]
    [Route(RouteApiConstant.BASE_PATH + "/material")]
    [Authorize]
    public class MaterialController : BaseAPIController
    {

        public readonly IMaterialService _materialService;
        private readonly IDataService _dataService;
        MaterialController(IMaterialService materialService, IDataService dataService)
        {
            _materialService = materialService;
            _dataService = dataService;
        }

        [HttpGet("list-material")]
        public async Task<ResponseDTO<List<Material>>> GetListMaterial([FromQuery] MaterialSearchDTO model)
        {
            model.ActionBy = UserId;
            var result = await HandleException(_dataService.ListMaterial(model), Message.MaterialMessage.SEARCH_SUCCESS);
            result.Meta = new ResponseMeta() { Total = model.Total, Index = model.PageIndex, PageSize = model.PageSize };
            return result;
        }

        [HttpGet("detail-material")]
        public async Task<ResponseDTO<Material>> DetailMaterial([FromQuery] int materialId)
        {
            return await HandleException(_materialService.GetDetailMaterial(materialId, UserId), Message.MaterialMessage.GET_DETAIL_SUCCESS);
        }

        [HttpPost("create-material")]
        public async Task<ResponseDTO<Material>> CreateMaterial([FromBody] MaterialCreateDTO model)
        {
            return await HandleException(_materialService.CreateMaterial(model, UserId), Message.MaterialMessage.CREATE_SUCCESS);
        }

        [HttpPut("update-material")]
        public async Task<ResponseDTO<Material>> UpdateMaterial([FromBody] Material model)
        {
            return await HandleException(_materialService.UpdateMaterial(model, UserId), Message.MaterialMessage.SAVE_SUCCESS);
        }

        [HttpDelete("delete-material/{materialId}")]
        public async Task<ResponseDTO<int>> DeleteMaterial(int materialId)
        {
            return await HandleException(_materialService.DeleteMaterial(materialId, UserId), Message.MaterialMessage.DELETE_SUCCESS);
        }
    }
}
