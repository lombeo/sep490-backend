using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sep490_Backend.DTO.Admin;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Services.DataService;

namespace Sep490_Backend.Controllers
{
    [ApiController]
    [Route(RouteApiConstant.BASE_PATH + "/common")]
    [Authorize]
    public class CommonController : BaseAPIController
    {
        private readonly IDataService _dataService;

        public CommonController(IDataService dataService)
        {
            _dataService = dataService;
        }

        [HttpGet("list-user")]
        public async Task<ResponseDTO<List<User>>> ListUser([FromQuery] AdminSearchUserDTO model)
        {
            model.ActionBy = UserId;
            var result = await HandleException(_dataService.ListUser(model), Message.AdminMessage.SEARCH_SUCCESS);
            result.Meta = new ResponseMeta() { Total = model.Total, Index = model.PageIndex, PageSize = model.PageSize };
            return result;
        }
    }
}