using Microsoft.AspNetCore.Http;
using Sep490_Backend.Infra.Enums;

namespace Sep490_Backend.DTO.ConstructionPlan
{
    public class ConstructionPlanQuery : BaseQuery
    {
        public string? PlanName { get; set; }
        public int? ProjectId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public bool? IsApproved { get; set; }
    }

    public class SaveConstructionPlanDTO : BaseRequest
    {
        public int? Id { get; set; }
        public string PlanName { get; set; }
        public int ProjectId { get; set; }
        public List<SaveConstructPlanItemDTO> PlanItems { get; set; } = new List<SaveConstructPlanItemDTO>();
        public List<int> ReviewerIds { get; set; } = new List<int>();
    }

    public class SaveConstructPlanItemDTO
    {
        public string WorkCode { get; set; }
        public string Index { get; set; }
        public string? ParentIndex { get; set; }
        public string WorkName { get; set; }
        public string Unit { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<int>? QAIds { get; set; }
        public Dictionary<string, string> ItemRelations { get; set; } = new Dictionary<string, string>();
        public List<SaveConstructPlanItemDetailDTO> Details { get; set; } = new List<SaveConstructPlanItemDetailDTO>();
    }

    public class SaveConstructPlanItemDetailDTO
    {
        public int? Id { get; set; }
        public string WorkCode { get; set; }
        public ResourceType ResourceType { get; set; }
        public int Quantity { get; set; }
        public string? Unit { get; set; }
        public decimal UnitPrice { get; set; }
        public int? ResourceId { get; set; }
    }

    public class ApproveConstructionPlanDTO : BaseRequest
    {
        public int PlanId { get; set; }
        public bool IsApproved { get; set; }
        public string? RejectReason { get; set; }
    }

    public class AssignTeamDTO : BaseRequest
    {
        public int PlanId { get; set; }
        public string WorkCode { get; set; }
        public List<int> TeamIds { get; set; } = new List<int>();
    }

    public class ImportConstructionPlanDTO : BaseRequest
    {
        public int ProjectId { get; set; }
        public string PlanName { get; set; }
        public IFormFile ExcelFile { get; set; }
        public List<int> ReviewerIds { get; set; } = new List<int>();
    }
} 