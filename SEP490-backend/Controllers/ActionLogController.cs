using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sep490_Backend.DTO.ActionLog;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Services.ActionLogService;

namespace Sep490_Backend.Controllers
{
    [Route(RouteApiConstant.BASE_PATH + "/actionlog")]
    [ApiController]
    [Authorize]
    public class ActionLogController : BaseAPIController
    {
        private readonly IActionLogService _actionLogService;
        private readonly ILogger<ActionLogController> _logger;

        public ActionLogController(
            IActionLogService actionLogService,
            ILogger<ActionLogController> logger)
        {
            _actionLogService = actionLogService;
            _logger = logger;
        }

        /// <summary>
        /// Get action log by ID
        /// </summary>
        /// <param name="id">ActionLog ID</param>
        /// <returns>ActionLog details</returns>
        [HttpGet("get-by-id/{id}")]
        [ResponseCache(Duration = 60)]
        public async Task<ResponseDTO<ActionLogDTO>> GetById(int id)
        {
            _logger.LogInformation("Getting action log with ID: {id}", id);
            return await HandleException(_actionLogService.GetByIdAsync(id), Message.ActionLogMessage.GET_DETAIL_SUCCESS);
        }

        /// <summary>
        /// Get paginated list of action logs with filtering
        /// </summary>
        /// <param name="query">Query parameters for filtering and pagination</param>
        /// <returns>Paginated list of action logs</returns>
        [HttpGet("search")]
        [ResponseCache(Duration = 30, VaryByQueryKeys = new[] { "PageIndex", "PageSize", "LogType", "SearchTerm", "FromDate", "ToDate" })]
        public async Task<ResponseDTO<List<ActionLogDTO>>> Search([FromQuery] ActionLogQuery query)
        {
            _logger.LogInformation("Searching action logs with query parameters");
            query.ActionBy = UserId;
            var result = await HandleException(_actionLogService.GetAllAsync(query), Message.ActionLogMessage.SEARCH_SUCCESS);
            result.Meta = new ResponseMeta() { Total = query.Total, Index = query.PageIndex, PageSize = query.PageSize };
            return result;
        }

        /// <summary>
        /// Create a new action log
        /// </summary>
        /// <param name="dto">Action log creation data</param>
        /// <returns>Created action log</returns>
        [HttpPost("create")]
        public async Task<ResponseDTO<ActionLogDTO>> Create([FromBody] ActionLogCreateDTO dto)
        {
            _logger.LogInformation("Creating new action log by user {userId}", UserId);
            return await HandleException(_actionLogService.CreateAsync(dto, UserId), Message.ActionLogMessage.CREATE_SUCCESS);
        }

        /// <summary>
        /// Update an existing action log
        /// </summary>
        /// <param name="dto">Updated action log data with ID</param>
        /// <returns>Updated action log</returns>
        [HttpPut("update/{id}")]
        public async Task<ResponseDTO<ActionLogDTO>> Update(int id, [FromBody] ActionLogUpdateDTO dto)
        {
            _logger.LogInformation("Updating action log {id} by user {userId}", id, UserId);
            return await HandleException(_actionLogService.UpdateAsync(id, dto, UserId), Message.ActionLogMessage.UPDATE_SUCCESS);
        }

        /// <summary>
        /// Delete an action log
        /// </summary>
        /// <param name="id">ActionLog ID</param>
        /// <returns>Success indicator</returns>
        [HttpDelete("{id}")]
        public async Task<ResponseDTO<bool>> Delete(int id)
        {
            _logger.LogInformation("Deleting action log with ID {id}", id);
            return await HandleException(_actionLogService.DeleteAsync(id), Message.ActionLogMessage.DELETE_SUCCESS);
        }

        /// <summary>
        /// Invalidate cache for action logs
        /// </summary>
        /// <param name="id">Optional specific log ID to invalidate</param>
        /// <returns>Success indicator</returns>
        [HttpPost("invalidate-cache")]
        [ResponseCache(NoStore = true)]
        public async Task<ResponseDTO<bool>> InvalidateCache([FromQuery] int? id = null)
        {
            _logger.LogInformation("Invalidating action log cache");
            return await HandleException(_actionLogService.InvalidateCacheAsync(id), Message.ActionLogMessage.CACHE_INVALIDATED);
        }
    }
} 