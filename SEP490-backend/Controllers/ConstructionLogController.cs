using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.ConstructionLog;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Services.ConstructionLogService;
using System.Threading.Tasks;

namespace Sep490_Backend.Controllers
{
    [Authorize]
    [Route(RouteApiConstant.BASE_PATH + "/ConstructionLog")]
    public class ConstructionLogController : BaseAPIController
    {
        private readonly IConstructionLogService _constructionLogService;

        public ConstructionLogController(IConstructionLogService constructionLogService)
        {
            _constructionLogService = constructionLogService;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _constructionLogService.GetByIdAsync(id);
            await _constructionLogService.CheckPermissionAsync(result.ProjectId, UserId);
            return Ok(await HandleException(Task.FromResult(result), Message.ConstructionLogMessage.GET_DETAIL_SUCCESS));
        }

        [HttpPost("query")]
        public async Task<IActionResult> Query([FromBody] ConstructionLogQueryDTO query)
        {
            if (query.ProjectId.HasValue)
            {
                await _constructionLogService.CheckPermissionAsync(query.ProjectId.Value, UserId);
            }
            
            query.ActionBy = UserId;
            var result = await _constructionLogService.GetAllAsync(query);
            return Ok(await HandleException(Task.FromResult(result), Message.ConstructionLogMessage.SEARCH_SUCCESS));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ConstructionLogDTO dto)
        {
            var result = await _constructionLogService.CreateAsync(dto, UserId);
            return Ok(await HandleException(Task.FromResult(result), Message.ConstructionLogMessage.CREATE_SUCCESS));
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] ConstructionLogDTO dto)
        {
            var result = await _constructionLogService.UpdateAsync(dto, UserId);
            return Ok(await HandleException(Task.FromResult(result), Message.ConstructionLogMessage.UPDATE_SUCCESS));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _constructionLogService.DeleteAsync(id, UserId);
            return Ok(await HandleException(Task.FromResult(result), Message.ConstructionLogMessage.DELETE_SUCCESS));
        }
    }
} 