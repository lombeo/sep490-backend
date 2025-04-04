using Microsoft.AspNetCore.Http;
using Sep490_Backend.Infra.Enums;
using System.ComponentModel.DataAnnotations;

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
        
        [Required(ErrorMessage = "Construction plan name is required")]
        public string PlanName { get; set; } = string.Empty;
        
        public int ProjectId { get; set; }
        public List<SaveConstructPlanItemDTO> PlanItems { get; set; } = new List<SaveConstructPlanItemDTO>();
        public List<int> ReviewerIds { get; set; } = new List<int>();
    }

    public class SaveConstructPlanItemDTO
    {
        public string WorkCode { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Index is required for proper ordering of construction tasks")]
        public string Index { get; set; } = string.Empty;
        
        public string? ParentIndex { get; set; }
        
        [Required(ErrorMessage = "Work name is required for site leveling activity")]
        public string WorkName { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Unit of measurement is required for construction materials")]
        public string Unit { get; set; } = string.Empty;
        
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public Dictionary<string, string> ItemRelations { get; set; } = new Dictionary<string, string>();
        public List<SaveConstructPlanItemDetailDTO> Details { get; set; } = new List<SaveConstructPlanItemDetailDTO>();
    }

    public class SaveConstructPlanItemDetailDTO
    {
        public int? Id { get; set; }
        
        public string WorkCode { get; set; } = string.Empty;
        
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
        
        [Required(ErrorMessage = "Work code is required for team assignment")]
        public string WorkCode { get; set; } = string.Empty;
        
        public List<int> TeamIds { get; set; } = new List<int>();
    }

    public class ImportConstructionPlanDTO : BaseRequest
    {
        public int ProjectId { get; set; }
        
        [Required(ErrorMessage = "Plan name is required for construction schedule")]
        public string PlanName { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Excel file with construction plan is required")]
        public IFormFile ExcelFile { get; set; } = null!;
        
        public List<int> ReviewerIds { get; set; } = new List<int>();
    }
} 