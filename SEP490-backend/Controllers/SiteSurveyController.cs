using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sep490_Backend.DTO.Admin;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.SiteSurvey;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Services.DataService;
using Sep490_Backend.Services.SiteSurveyService;

namespace Sep490_Backend.Controllers
{
    [ApiController]
    [Route(RouteApiConstant.BASE_PATH + "/survey")]
    [Authorize]
    public class SiteSurveyController : BaseAPIController
    {
        private readonly ISiteSurveyService _siteSurveyService;
        private readonly IDataService _dataService;
        public SiteSurveyController(ISiteSurveyService siteSurveyService, IDataService dataService)
        {
            _siteSurveyService = siteSurveyService;
            _dataService = dataService;
        }

        [HttpGet("search")]
        public async Task<ResponseDTO<List<SiteSurvey>>> Search([FromQuery] SearchSiteSurveyDTO model)
        {
            model.ActionBy = UserId;
            var result = await HandleException(_dataService.ListSiteSurvey(model), Message.SiteSurveyMessage.SEARCH_SUCCESS);
            result.Meta = new ResponseMeta() { Total = model.Total, Index = model.PageIndex, PageSize = model.PageSize };
            return result;
        }

        [HttpDelete("delete/{id}")]
        public async Task<ResponseDTO<int>> DeleteSiteSurvey(int id)
        {
            var result = await HandleException(_siteSurveyService.DeleteSiteSurvey(id, UserId), Message.SiteSurveyMessage.DELETE_SUCCESS);
            return result;
        }

        [HttpPost("save")]
        public async Task<ResponseDTO<SiteSurvey>> SaveSiteSurvey([FromForm] SiteSurvey model, [FromForm] List<IFormFile> attachments = null)
        {
            var result = await HandleException(_siteSurveyService.SaveSiteSurvey(model, UserId, attachments), Message.SiteSurveyMessage.SAVE_SUCCESS);
            return result;
        }

        [HttpGet("detail")]
        public async Task<ResponseDTO<SiteSurvey>> GetSiteSurveyDetail([FromQuery] int id)
        {
            var result = await HandleException(_siteSurveyService.GetSiteSurveyDetail(id, UserId), Message.SiteSurveyMessage.GET_DETAIL_SUCCESS);
            return result;
        }
    }
}
