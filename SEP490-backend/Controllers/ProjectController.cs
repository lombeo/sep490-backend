using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.Project;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Services.DataService;
using Sep490_Backend.Services.ProjectService;
using Microsoft.Extensions.Logging;

namespace Sep490_Backend.Controllers
{
    [ApiController]
    [Route(RouteApiConstant.BASE_PATH + "/project")]
    [Authorize]
    public class ProjectController : BaseAPIController
    {
        private readonly IProjectService _projectService;
        private readonly IDataService _dataService;
        private readonly ILogger<ProjectController> _logger;

        public ProjectController(IProjectService projectService, IDataService dataService, ILogger<ProjectController> logger)
        {
            _projectService = projectService;
            _dataService = dataService;
            _logger = logger;
        }

        [HttpGet("list")]
        public async Task<ResponseDTO<List<ProjectDTO>>> List([FromQuery] SearchProjectDTO model)
        {
            model.ActionBy = UserId;
            var result = await HandleException(_dataService.ListProject(model), Message.ProjectMessage.SEARCH_SUCCESS);
            result.Meta = new ResponseMeta() { Total = model.Total, Index = model.PageIndex, PageSize = model.PageSize };
            return result;
        }

        [HttpPost("save")]
        [Consumes("multipart/form-data")]
        public async Task<ResponseDTO<ProjectDTO>> Save([FromForm] SaveProjectDTO model)
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
            var result = await HandleException(_projectService.Detail(id, UserId), Message.ProjectMessage.GET_DETAIL_SUCCESS);
            return result;
        }

        /// <summary>
        /// Update a project's status (Executive Board only)
        /// </summary>
        /// <param name="model">Update status model</param>
        /// <returns>Success indicator</returns>
        [HttpPut("update-status")]
        public async Task<ResponseDTO<bool>> UpdateStatus([FromBody] UpdateProjectStatusDTO model)
        {
            _logger.LogInformation($"Updating project {model.ProjectId} status to {model.TargetStatus}");
            return await HandleException(_projectService.UpdateStatus(model, UserId), Message.ProjectMessage.UPDATE_STATUS_SUCCESS);
        }

        /// <summary>
        /// Get statistics for all projects the current user participates in
        /// </summary>
        /// <returns>Project statistics (planned, inprogress, inspected, remain)</returns>
        [HttpGet("statistic")]
        public async Task<ResponseDTO<ProjectStatisticsDTO>> GetUserProjectStatistics()
        {
            _logger.LogInformation($"Getting project statistics for user {UserId}");
            return await HandleException(_projectService.GetUserProjectStatistics(UserId), Message.ProjectMessage.STATISTICS_SUCCESS);
        }

        /// <summary>
        /// Get statistics for a specific project
        /// </summary>
        /// <param name="projectId">ID of the project</param>
        /// <returns>Project statistics (planned, inprogress, inspected, remain)</returns>
        [HttpGet("statistic/{projectId}")]
        public async Task<ResponseDTO<ProjectStatisticsDTO>> GetProjectStatistics(int projectId)
        {
            _logger.LogInformation($"Getting statistics for project {projectId}");
            return await HandleException(_projectService.GetProjectStatistics(projectId, UserId), Message.ProjectMessage.STATISTICS_SUCCESS);
        }
    }
}
