using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.ConstructionPlan;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Services.ConstructionPlanService;

namespace Sep490_Backend.Controllers
{
    [Route(RouteApiConstant.BASE_PATH + "/plans")]
    [ApiController]
    [Authorize]
    public class ConstructionPlanController : BaseAPIController
    {
        private readonly IConstructionPlanService _constructionPlanService;

        public ConstructionPlanController(IConstructionPlanService constructionPlanService)
        {
            _constructionPlanService = constructionPlanService;
        }

        [HttpPost]
        [Route("search")]
        public async Task<ResponseDTO<List<ConstructionPlanDTO>>> Search([FromQuery] ConstructionPlanQuery query)
        {
            query.ActionBy = UserId;
            var response = await HandleException(_constructionPlanService.Search(query), Message.ConstructionPlanMessage.SEARCH_SUCCESS);
            response.Meta = new ResponseMeta() { Total = query.Total, Index = query.PageIndex, PageSize = query.PageSize };
            return response;
        }

        [HttpGet("detail/{id}")]
        public async Task<ResponseDTO<ConstructionPlanDTO>> GetById(int id)
        {
            return await HandleException(_constructionPlanService.GetById(id, UserId), Message.ConstructionPlanMessage.GET_DETAIL_SUCCESS);
        }

        [HttpPost("create")]
        public async Task<ResponseDTO<ConstructionPlanDTO>> Create([FromBody] SaveConstructionPlanDTO model)
        {
            return await HandleException(_constructionPlanService.Create(model, UserId), Message.ConstructionPlanMessage.CREATE_SUCCESS);
        }

        [HttpPut("update")]
        public async Task<ResponseDTO<ConstructionPlanDTO>> Update([FromBody] SaveConstructionPlanDTO model)
        {
            return await HandleException(_constructionPlanService.Update(model, UserId), Message.ConstructionPlanMessage.UPDATE_SUCCESS);
        }

        [HttpDelete("{id}")]
        public async Task<ResponseDTO<bool>> Delete(int id)
        {
            return await HandleException(_constructionPlanService.Delete(id, UserId), Message.ConstructionPlanMessage.DELETE_SUCCESS);
        }

        [HttpPost("approve")]
        public async Task<ResponseDTO<bool>> Approve([FromBody] ApproveConstructionPlanDTO model)
        {
            model.IsApproved = true;
            return await HandleException(_constructionPlanService.Approve(model, UserId), Message.ConstructionPlanMessage.APPROVE_SUCCESS);
        }

        [HttpPost("reject")]
        public async Task<ResponseDTO<bool>> Reject([FromBody] ApproveConstructionPlanDTO model)
        {
            model.IsApproved = false;
            return await HandleException(_constructionPlanService.Reject(model, UserId), Message.ConstructionPlanMessage.REJECT_SUCCESS);
        }

        [HttpPost("import")]
        public async Task<ResponseDTO<ConstructionPlanDTO>> Import([FromForm] ImportConstructionPlanDTO model)
        {
            return await HandleException(_constructionPlanService.Import(model, UserId), Message.ConstructionPlanMessage.IMPORT_SUCCESS);
        }

        [HttpPost("assign-team")]
        public async Task<ResponseDTO<bool>> AssignTeam([FromBody] AssignTeamDTO model)
        {
            return await HandleException(_constructionPlanService.AssignTeam(model, UserId), Message.ConstructionPlanMessage.ASSIGN_TEAM_SUCCESS);
        }
    }
} 