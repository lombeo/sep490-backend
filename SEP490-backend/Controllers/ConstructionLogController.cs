using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.ConstructionLog;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Services.ConstructionLogService;

namespace Sep490_Backend.Controllers
{
    [ApiController]
    [Route(RouteApiConstant.BASE_PATH + "/construction-log")]
    [Authorize]
    public class ConstructionLogController : BaseAPIController
    {
        private readonly IConstructionLogService _constructionLogService;

        public ConstructionLogController(IConstructionLogService constructionLogService)
        {
            _constructionLogService = constructionLogService;
        }

        [HttpGet("list")]
        public async Task<ResponseDTO<List<ConstructionLogDTO>>> List([FromQuery] SearchConstructionLogDTO model)
        {
            model.ActionBy = UserId;
            var result = await HandleException(_constructionLogService.List(model), Message.ConstructionLogMessage.SEARCH_SUCCESS);
            return result;
        }

        [HttpGet("{id}")]
        public async Task<ResponseDTO<ConstructionLogDTO>> Detail(int id)
        {
            var result = await HandleException(_constructionLogService.Detail(id, UserId), Message.ConstructionLogMessage.GET_DETAIL_SUCCESS);
            return result;
        }

        [HttpGet("project/{projectId}")]
        public async Task<ResponseDTO<List<ConstructionLogDTO>>> GetByProject(int projectId)
        {
            var result = await HandleException(_constructionLogService.GetByProject(projectId, UserId), Message.ConstructionLogMessage.GET_BY_PROJECT_SUCCESS);
            return result;
        }

        [HttpGet("project/{projectId}/task/{taskIndex}")]
        public async Task<ResponseDTO<ResourceLogByTaskDTO>> GetResourceLogByTask(int projectId, string taskIndex)
        {
            var result = await HandleException(_constructionLogService.GetResourceLogByTask(projectId, taskIndex, UserId), Message.ConstructionLogMessage.GET_BY_TASK_SUCCESS);
            return result;
        }

        [HttpPost("save")]
        [Consumes("multipart/form-data")]
        public async Task<ResponseDTO<ConstructionLogDTO>> Save([FromForm] SaveConstructionLogDTO model)
        {
            var successMessage = model.Id == 0 ? Message.ConstructionLogMessage.CREATE_SUCCESS : Message.ConstructionLogMessage.UPDATE_SUCCESS;
            var result = await HandleException(_constructionLogService.Save(model, UserId), successMessage);
            return result;
        }

        [HttpPut("{id}/approve")]
        public async Task<ResponseDTO<ConstructionLogDTO>> Approve(int id)
        {
            var model = new SaveConstructionLogDTO
            {
                Id = id,
                Status = ConstructionLogStatus.Approved
            };
            
            var result = await HandleException(_constructionLogService.Save(model, UserId), Message.ConstructionLogMessage.APPROVE_SUCCESS);
            return result;
        }

        [HttpPut("{id}/reject")]
        public async Task<ResponseDTO<ConstructionLogDTO>> Reject(int id)
        {
            var model = new SaveConstructionLogDTO
            {
                Id = id,
                Status = ConstructionLogStatus.Rejected
            };
            
            var result = await HandleException(_constructionLogService.Save(model, UserId), Message.ConstructionLogMessage.REJECT_SUCCESS);
            return result;
        }

        [HttpDelete("{id}")]
        public async Task<ResponseDTO<int>> Delete(int id)
        {
            var result = await HandleException(_constructionLogService.Delete(id, UserId), Message.ConstructionLogMessage.DELETE_SUCCESS);
            return result;
        }
    }
} 