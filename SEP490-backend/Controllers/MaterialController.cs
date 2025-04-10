using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.Material;
using Sep490_Backend.DTO.SiteSurvey;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Services.DataService;
using Sep490_Backend.Services.MaterialService;
using Sep490_Backend.Services.SiteSurveyService;
using Microsoft.Extensions.Logging;

namespace Sep490_Backend.Controllers
{
    [ApiController]
    [Route(RouteApiConstant.BASE_PATH + "/material")]
    [Authorize]
    public class MaterialController : BaseAPIController
    {
        private readonly IMaterialService _materialService;
        private readonly IDataService _dataService;
        private readonly ILogger<MaterialController> _logger;

        public MaterialController(IMaterialService materialService, IDataService dataService, ILogger<MaterialController> logger)
        {
            _materialService = materialService;
            _dataService = dataService;
            _logger = logger;
        }

        [HttpGet("search")]
        public async Task<ResponseDTO<List<Material>>> Search([FromQuery] MaterialSearchDTO model)
        {
            model.ActionBy = UserId;
            var result = await HandleException(_dataService.ListMaterial(model), Message.CommonMessage.ACTION_SUCCESS);
            result.Meta = new ResponseMeta() { Total = model.Total, Index = model.PageIndex, PageSize = model.PageSize };
            return result;
        }

        [HttpGet("detail/{id}")]
        public async Task<ResponseDTO<MaterialDetailDTO>> GetMaterialById(int id)
        {
            _logger.LogInformation($"Getting material details for material ID: {id}");
            return await HandleException(_materialService.GetMaterialById(id, UserId), Message.MaterialMessage.GET_DETAIL_SUCCESS);
        }

        [HttpDelete("delete/{id}")]
        public async Task<ResponseDTO<bool>> DeleteMaterial(int id)
        {
            _logger.LogInformation($"Deleting material with ID: {id}");
            return await HandleException(_materialService.DeleteMaterial(id, UserId), Message.MaterialMessage.DELETE_SUCCESS);
        }

        [HttpPost("save")]
        public async Task<ResponseDTO<Material>> SaveMaterial([FromBody] MaterialSaveDTO model)
        {
            _logger.LogInformation($"Saving material: {model.MaterialCode}");
            return await HandleException(_materialService.SaveMaterial(model, UserId), Message.MaterialMessage.SAVE_SUCCESS);
        }
    }
}
