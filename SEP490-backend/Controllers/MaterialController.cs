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

namespace Sep490_Backend.Controllers
{
    [ApiController]
    [Route(RouteApiConstant.BASE_PATH + "/material")]
    [Authorize]
    public class MaterialController : BaseAPIController
    {
        private readonly IMaterialService _materialService;
        private readonly IDataService _dataService;
        public MaterialController(IMaterialService materialService, IDataService dataService)
        {
            _materialService = materialService;
            _dataService = dataService;
        }
        [HttpGet("search")]
        public async Task<ResponseDTO<List<Material>>> Search([FromQuery] MaterialSearchDTO model)
        {
            model.ActionBy = UserId;
            var result = await HandleException(_dataService.ListMaterial(model), Message.CommonMessage.ACTION_SUCCESS);
            result.Meta = new ResponseMeta() { Total = model.Total, Index = model.PageIndex, PageSize = model.PageSize };
            return result;
        }

        [HttpDelete("delete/{id}")]
        public async Task<ResponseDTO<bool>> Delete(int id)
        {
            var result = await HandleException(_materialService.DeleteMaterial(id, UserId), Message.SiteSurveyMessage.DELETE_SUCCESS);
            return result;
        }

        [HttpPost("save")]
        public async Task<ResponseDTO<Material>> SaveSiteSurvey([FromForm] Material model)
        {
            var result = await HandleException(_materialService.SaveMaterial(model, UserId), Message.SiteSurveyMessage.SAVE_SUCCESS);
            return result;
        }
    }
}
