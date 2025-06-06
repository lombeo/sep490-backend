﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sep490_Backend.DTO.Admin;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Services.AdminService;
using Sep490_Backend.Services.DataService;

namespace Sep490_Backend.Controllers
{
    [ApiController]
    [Route(RouteApiConstant.BASE_PATH + "/admin")]
    [Authorize]
    public class AdminController : BaseAPIController
    {
        private readonly IAdminService _adminService;
        private readonly IDataService _dataService;

        public AdminController(IAdminService adminService, IDataService dataService)
        {
            _adminService = adminService;
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

        [HttpDelete("delete-user/{userId}")]
        public async Task<ResponseDTO<bool>> DeleteUser(int userId)
        {
            return await HandleException(_adminService.DeleteUser(userId, UserId), Message.AdminMessage.DELETE_USER_SUCCESS);
        }


        [HttpPost("create-user")]
        public async Task<ResponseDTO<User>> CreateUser([FromBody] AdminCreateUserDTO model)
        {
            return await HandleException(_adminService.CreateUser(model, UserId), Message.AdminMessage.CREATE_USER_SUCCESS);
        }

        [HttpPut("update-user")]
        public async Task<ResponseDTO<User>> UpdateUser([FromBody] AdminUpdateUserDTO model)
        {
            return await HandleException(_adminService.UpdateUser(model, UserId), Message.AdminMessage.UPDATE_USER_SUCCESS);
        }
    }
}
