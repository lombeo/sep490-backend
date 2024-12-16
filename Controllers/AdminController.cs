using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sep490_Backend.DTO;
using Sep490_Backend.DTO.AdminDTO;
using Sep490_Backend.DTO.AuthenDTO;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Services.AdminService;
using Sep490_Backend.Services.AuthenService;
using Sprache;

namespace Sep490_Backend.Controllers
{
    [ApiController]
    [Route(RouteApiConstant.BASE_PATH + "/admin")]
    [Authorize]
    public class AdminController : BaseAPIController
    {
        private readonly IAdminService _adminService;

        public AdminController(IAdminService adminService)
        {
            _adminService = adminService;
        }

        [HttpPost("list-user")]
        public async Task<ResponseDTO<List<UserDTO>>> ListUser([FromQuery] AdminSearchUserDTO model)
        {
            model.ActionBy = UserId;
            var result = await HandleException(_adminService.ListUser(model), Message.AdminMessage.SEARCH_SUCCESS);
            result.Meta = new ResponseMeta() { Total = model.Total, Index = model.PageIndex, PageSize = model.PageSize };
            return result;
        }
    }
}
