using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.ConstructionTeam;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Services.ConstructionTeamService;
using Sep490_Backend.Services.DataService;

namespace Sep490_Backend.Controllers
{
    [ApiController]
    [Route(RouteApiConstant.BASE_PATH + "/teams")]
    [Authorize]
    public class ConstructionTeamController : BaseAPIController
    {
        private readonly IConstructionTeamService _constructionTeamService;
        private readonly IDataService _dataService;

        public ConstructionTeamController(IConstructionTeamService constructionTeamService, IDataService dataService)
        {
            _constructionTeamService = constructionTeamService;
            _dataService = dataService;
        }

        [HttpGet("search")]
        public async Task<ResponseDTO<List<ConstructionTeam>>> Search([FromQuery] ConstructionTeamSearchDTO model)
        {
            model.ActionBy = UserId;
            var result = await HandleException(_dataService.ListConstructionTeam(model), Message.ConstructionTeamMessage.SEARCH_SUCCESS);
            result.Meta = new ResponseMeta() { Total = model.Total, Index = model.PageIndex, PageSize = model.PageSize };
            return result;
        }

        [HttpGet("detail/{id}")]
        public async Task<ResponseDTO<ConstructionTeam>> GetById(int id)
        {
            // Here you would normally have a GetById method in your service
            // For now, we'll use the List method to get a team by ID
            var model = new ConstructionTeamSearchDTO { ActionBy = UserId };
            var teams = await _dataService.ListConstructionTeam(model);
            var team = teams.FirstOrDefault(t => t.Id == id);
            
            if (team == null)
            {
                throw new KeyNotFoundException(Message.ConstructionTeamMessage.NOT_FOUND);
            }
            
            return await HandleException(Task.FromResult(team), Message.ConstructionTeamMessage.GET_DETAIL_SUCCESS);
        }

        [HttpPost("save")]
        public async Task<ResponseDTO<ConstructionTeam>> Save([FromBody] ConstructionTeamSaveDTO model)
        {
            return await HandleException(_constructionTeamService.Save(model, UserId), Message.ConstructionTeamMessage.SAVE_SUCCESS);
        }

        [HttpDelete("delete/{id}")]
        public async Task<ResponseDTO<bool>> Delete(int id)
        {
            return await HandleException(_constructionTeamService.Delete(id, UserId), Message.ConstructionTeamMessage.DELETE_SUCCESS);
        }

        [HttpDelete("remove-member-from-team/{id}")]
        public async Task<ResponseDTO<bool>> RemoveMemberFromTeam(int id)
        {
            return await HandleException(_constructionTeamService.RemoveMemberFromTeam(id, UserId), Message.ConstructionTeamMessage.REMOVE_MEMBER_SUCCESS);
        }
    }
} 