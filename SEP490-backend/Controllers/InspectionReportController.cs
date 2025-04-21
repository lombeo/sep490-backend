using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.InspectionReport;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Enums;
using Sep490_Backend.Services.InspectionReportService;

namespace Sep490_Backend.Controllers
{
    [ApiController]
    [Route(RouteApiConstant.BASE_PATH + "/inspection-report")]
    [Authorize]
    public class InspectionReportController : BaseAPIController
    {
        private readonly IInspectionReportService _inspectionReportService;

        public InspectionReportController(IInspectionReportService inspectionReportService)
        {
            _inspectionReportService = inspectionReportService;
        }

        [HttpGet("list")]
        public async Task<ResponseDTO<List<InspectionReportDTO>>> List([FromQuery] SearchInspectionReportDTO model)
        {
            model.ActionBy = UserId;
            var result = await HandleException(_inspectionReportService.List(model), Message.InspectionReportMessage.SEARCH_SUCCESS);
            return result;
        }

        [HttpGet("{id}")]
        public async Task<ResponseDTO<InspectionReportDTO>> Detail(int id)
        {
            var result = await HandleException(_inspectionReportService.Detail(id, UserId), Message.InspectionReportMessage.GET_DETAIL_SUCCESS);
            return result;
        }

        [HttpGet("project/{projectId}")]
        public async Task<ResponseDTO<List<InspectionReportDTO>>> GetByProject(int projectId)
        {
            var result = await HandleException(_inspectionReportService.GetByProject(projectId, UserId), Message.InspectionReportMessage.GET_BY_PROJECT_SUCCESS);
            return result;
        }

        [HttpPost("save")]
        [Consumes("multipart/form-data")]
        public async Task<ResponseDTO<InspectionReportDTO>> Save([FromForm] SaveInspectionReportDTO model)
        {
            var successMessage = model.Id == 0 ? Message.InspectionReportMessage.CREATE_SUCCESS : Message.InspectionReportMessage.UPDATE_SUCCESS;
            var result = await HandleException(_inspectionReportService.Save(model, UserId), successMessage);
            return result;
        }

        [HttpPut("approve/{id}")]
        public async Task<ResponseDTO<InspectionReportDTO>> Approve(int id)
        {
            var model = new SaveInspectionReportDTO
            {
                Id = id,
                Status = InspectionReportStatus.Approved
            };
            
            var result = await HandleException(_inspectionReportService.Save(model, UserId), Message.InspectionReportMessage.APPROVE_SUCCESS);
            return result;
        }

        [HttpPut("reject/{id}")]
        public async Task<ResponseDTO<InspectionReportDTO>> Reject(int id)
        {
            var model = new SaveInspectionReportDTO
            {
                Id = id,
                Status = InspectionReportStatus.Rejected
            };
            
            var result = await HandleException(_inspectionReportService.Save(model, UserId), Message.InspectionReportMessage.REJECT_SUCCESS);
            return result;
        }

        [HttpDelete("{id}")]
        public async Task<ResponseDTO<int>> Delete(int id)
        {
            var result = await HandleException(_inspectionReportService.Delete(id, UserId), Message.InspectionReportMessage.DELETE_SUCCESS);
            return result;
        }
    }
} 