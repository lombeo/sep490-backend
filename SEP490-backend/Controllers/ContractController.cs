using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sep490_Backend.DTO.Admin;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.Contract;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Services.ContractService;
using Sep490_Backend.Services.DataService;

namespace Sep490_Backend.Controllers
{
    [ApiController]
    [Route(RouteApiConstant.BASE_PATH + "/contract")]
    [Authorize]
    public class ContractController : BaseAPIController
    {
        private readonly IContractService _contractService;
        private readonly IDataService _dataService;

        public ContractController(IContractService contractService, IDataService dataService)
        {
            _contractService = contractService;
            _dataService = dataService;
        }

        [HttpGet("list-contract")]
        public async Task<ResponseDTO<List<ContractDTO>>> ListContract([FromQuery] SearchContractDTO model)
        {
            model.ActionBy = UserId;
            var result = await HandleException(_dataService.ListContract(model), Message.ContractMessage.SEARCH_SUCCESS);
            result.Meta = new ResponseMeta() { Total = model.Total, Index = model.PageIndex, PageSize = model.PageSize };
            return result;
        }

        [HttpDelete("delete-contract/{projectId}")]
        public async Task<ResponseDTO<int>> DeleteContract(int projectId)
        {
            return await HandleException(_contractService.Delete(projectId, UserId), Message.ContractMessage.DELETE_SUCCESS);
        }

        [HttpPost("save-contract")]
        public async Task<ResponseDTO<ContractDTO>> SaveContract([FromForm] SaveContractDTO model)
        {
            model.ActionBy = UserId;
            return await HandleException(_contractService.Save(model), Message.ContractMessage.SAVE_SUCCESS);
        }

        [HttpGet("detail/{projectId}")]
        public async Task<ResponseDTO<ContractDTO>> DetailContract(int projectId)
        {
            var result = await HandleException(_contractService.Detail(projectId, UserId), Message.ContractMessage.SEARCH_SUCCESS);
            return result;
        }
    }
}
