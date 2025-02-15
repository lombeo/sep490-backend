using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.ProjectDTO;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Services.ProjectService;

namespace Sep490_Backend.Controllers
{
    [ApiController]
    [Route(RouteApiConstant.BASE_PATH + "/project")]
    [Authorize]
    public class ProjectController : BaseAPIController
    {
        private readonly IProjectService _projectService;

        public ProjectController(IProjectService projectService)
        {
            _projectService = projectService;
        }

        [HttpGet("list")]
        public async Task<ResponseDTO<List<ProjectDTO>>> List([FromQuery] SearchProjectDTO model)
        {
            model.ActionBy = UserId;
            var result = await HandleException(_projectService.List(model), Message.ProjectMessage.SEARCH_SUCCESS);
            result.Meta = new ResponseMeta() { Total = model.Total, Index = model.PageIndex, PageSize = model.PageSize };
            return result;
        }

        [HttpPost("save")]
        public async Task<ResponseDTO<ProjectDTO>> Save([FromBody] Project model)
        {
            var result = await HandleException(_projectService.Save(model, UserId), Message.ProjectMessage.SAVE_SUCCESS);
            return result;
        }

        [HttpDelete("delete/{id}")]
        public async Task<ResponseDTO<int>> Delete(int id)
        {
            var result = await HandleException(_projectService.Delete(id, UserId), Message.ProjectMessage.DELETE_SUCCESS);
            return result;
        }

        [HttpGet("list-project-status")]
        public async Task<ResponseDTO<ListProjectStatusDTO>> ListProjectStatus()
        {
            var result = await HandleException(_projectService.ListProjectStatus(UserId), Message.ProjectMessage.GET_LIST_STATUS_SUCCESS);
            return result;
        }

        [HttpGet("detail/{id}")]
        public async Task<ResponseDTO<ProjectDTO>> Detail(int id)
        {
            var result = await HandleException(_projectService.Detail(id ,UserId), Message.ProjectMessage.GET_DETAIL_SUCCESS);
            return result;
        }
    }
}
