using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.ConstructionLog;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Services.ConstructionLogService;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Sep490_Backend.Controllers
{
    [ApiController]
    [Route(RouteApiConstant.BASE_PATH + "/ConstructionLog")]
    [Authorize]
    public class ConstructionLogController : BaseAPIController
    {
        private readonly IConstructionLogService _constructionLogService;
        private readonly ILogger<ConstructionLogController> _logger;

        public ConstructionLogController(IConstructionLogService constructionLogService, ILogger<ConstructionLogController> logger)
        {
            _constructionLogService = constructionLogService;
            _logger = logger;
        }

        [HttpGet("{id}")]
        public async Task<ResponseDTO<ConstructionLogDTO>> GetById(int id)
        {
            _logger.LogInformation($"Getting construction log with ID: {id}");
            await _constructionLogService.CheckPermissionAsync(
                (await _constructionLogService.GetByIdAsync(id)).ProjectId, UserId);
            return await HandleException(_constructionLogService.GetByIdAsync(id), Message.ConstructionLogMessage.GET_DETAIL_SUCCESS);
        }

        [HttpPost("query")]
        public async Task<ResponseDTO<List<ConstructionLogDTO>>> Query([FromBody] ConstructionLogQueryDTO query)
        {
            _logger.LogInformation($"Querying construction logs");
            if (query.ProjectId.HasValue)
            {
                await _constructionLogService.CheckPermissionAsync(query.ProjectId.Value, UserId);
            }
            
            query.ActionBy = UserId;
            var result = await HandleException(_constructionLogService.GetAllAsync(query), Message.ConstructionLogMessage.SEARCH_SUCCESS);
            result.Meta = new ResponseMeta() { Total = query.Total, Index = query.PageIndex, PageSize = query.PageSize };
            return result;
        }

        [HttpPost]
        public async Task<ResponseDTO<ConstructionLogDTO>> Create([FromBody] ConstructionLogDTO dto)
        {
            _logger.LogInformation($"Creating new construction log: {dto.LogName}");
            return await HandleException(_constructionLogService.CreateAsync(dto, UserId), Message.ConstructionLogMessage.CREATE_SUCCESS);
        }

        [HttpPut]
        public async Task<ResponseDTO<ConstructionLogDTO>> Update([FromBody] ConstructionLogDTO dto)
        {
            _logger.LogInformation($"Updating construction log with ID: {dto.Id}");
            return await HandleException(_constructionLogService.UpdateAsync(dto, UserId), Message.ConstructionLogMessage.UPDATE_SUCCESS);
        }

        [HttpDelete("{id}")]
        public async Task<ResponseDTO<bool>> Delete(int id)
        {
            _logger.LogInformation($"Deleting construction log with ID: {id}");
            return await HandleException(_constructionLogService.DeleteAsync(id, UserId), Message.ConstructionLogMessage.DELETE_SUCCESS);
        }
    }
} 