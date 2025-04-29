using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.ConstructionProgress;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Services.ConstructionProgressService;

namespace Sep490_Backend.Controllers
{
    [Route(RouteApiConstant.BASE_PATH + "/construction-progress")]
    [ApiController]
    [Authorize]
    public class ConstructionProgressController : BaseAPIController
    {
        private readonly IConstructionProgressService _constructionProgressService;
        private readonly ILogger<ConstructionProgressController> _logger;

        public ConstructionProgressController(
            IConstructionProgressService constructionProgressService,
            ILogger<ConstructionProgressController> logger)
        {
            _constructionProgressService = constructionProgressService;
            _logger = logger;
        }

        /// <summary>
        /// Get construction progress by ID
        /// </summary>
        /// <param name="id">Progress ID</param>
        /// <returns>Construction progress details</returns>
        [HttpGet("get-by-id/{id}")]
        [ResponseCache(Duration = 60)]
        public async Task<ResponseDTO<ConstructionProgressDTO>> GetById(int id)
        {
            _logger.LogInformation("Getting construction progress with ID: {id}", id);
            return await HandleException(_constructionProgressService.GetById(id, UserId), Message.ConstructionProgressMessage.GET_DETAIL_SUCCESS);
        }

        /// <summary>
        /// Get construction progress by project ID
        /// </summary>
        /// <param name="projectId">Project ID</param>
        /// <returns>Construction progress details</returns>
        [HttpGet("get-by-project/{projectId}")]
        [ResponseCache(Duration = 60)]
        public async Task<ResponseDTO<ConstructionProgressDTO>> GetByProjectId(int projectId)
        {
            _logger.LogInformation("Getting construction progress for project with ID: {projectId}", projectId);
            return await HandleException(_constructionProgressService.GetByProjectId(projectId, UserId), Message.ConstructionProgressMessage.GET_DETAIL_SUCCESS);
        }

        /// <summary>
        /// Search construction progress with filters
        /// </summary>
        /// <param name="query">Query parameters for filtering</param>
        /// <returns>List of construction progress matching the filters</returns>
        [HttpGet("search")]
        [ResponseCache(Duration = 30, VaryByQueryKeys = new[] { "ProjectId", "PlanId", "PageIndex", "PageSize" })]
        public async Task<ResponseDTO<List<ConstructionProgressDTO>>> Search([FromQuery] ConstructionProgressQuery query)
        {
            _logger.LogInformation("Searching construction progress with parameters: ProjectId={ProjectId}, PlanId={PlanId}", query.ProjectId, query.PlanId);
            query.ActionBy = UserId;
            var result = await HandleException(_constructionProgressService.Search(query), Message.ConstructionProgressMessage.SEARCH_SUCCESS);
            result.Meta = new ResponseMeta() { Total = query.Total, Index = query.PageIndex, PageSize = query.PageSize };
            return result;
        }

        /// <summary>
        /// Update progress for multiple items
        /// </summary>
        /// <param name="model">Update progress model</param>
        /// <returns>Success flag</returns>
        [HttpPut("update")]
        public async Task<ResponseDTO<bool>> Update([FromBody] UpdateProgressItemsDTO model)
        {
            _logger.LogInformation("Updating progress for {count} items in progress ID: {progressId}", model.Items.Count, model.ProgressId);
            return await HandleException(_constructionProgressService.Update(model, UserId), Message.ConstructionProgressMessage.UPDATE_SUCCESS);
        }

        /// <summary>
        /// Create a new construction progress item
        /// </summary>
        /// <param name="model">The progress item details</param>
        /// <returns>Created progress item</returns>
        /// <remarks>
        /// This endpoint is restricted to Technical Managers assigned to the project.
        /// The WorkCode is automatically generated based on the item's index.
        /// </remarks>
        [HttpPost("item/create")]
        public async Task<ResponseDTO<ProgressItemDTO>> CreateProgressItem([FromBody] SaveProgressItemDTO model)
        {
            _logger.LogInformation("Creating new progress item in progress ID: {progressId} with name: {workName}", 
                model.ProgressId, model.WorkName);
            
            return await HandleException(
                _constructionProgressService.CreateProgressItem(model, UserId), 
                Message.ConstructionProgressMessage.CREATE_SUCCESS);
        }

        /// <summary>
        /// Update an existing construction progress item
        /// </summary>
        /// <param name="model">The progress item update details</param>
        /// <returns>Updated progress item</returns>
        /// <remarks>
        /// This endpoint is restricted to Technical Managers assigned to the project.
        /// </remarks>
        [HttpPut("item/update")]
        public async Task<ResponseDTO<ProgressItemDTO>> UpdateProgressItem([FromBody] UpdateProgressItemDTO model)
        {
            _logger.LogInformation("Updating progress item ID: {id} in progress ID: {progressId}", 
                model.Id, model.ProgressId);
            
            return await HandleException(
                _constructionProgressService.UpdateProgressItem(model, UserId), 
                Message.ConstructionProgressMessage.UPDATE_SUCCESS);
        }
    }
} 