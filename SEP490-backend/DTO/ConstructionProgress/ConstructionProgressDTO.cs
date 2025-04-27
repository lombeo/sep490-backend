using Sep490_Backend.DTO.Common;
using Sep490_Backend.Infra.Enums;
using Sep490_Backend.DTO;

namespace Sep490_Backend.DTO.ConstructionProgress
{
    public class ConstructionProgressDTO
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public int PlanId { get; set; }
        public List<ProgressItemDTO> ProgressItems { get; set; } = new List<ProgressItemDTO>();
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? CreatedBy { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public int? UpdatedBy { get; set; }
        public string UpdatedByName { get; set; } = string.Empty;
    }

    public class ProgressItemDTO
    {
        public int Id { get; set; }
        public string WorkCode { get; set; } = string.Empty;
        public string Index { get; set; } = string.Empty;
        public string? ParentIndex { get; set; }
        public string WorkName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public int Progress { get; set; } = 0;
        public int Status { get; set; } = 0;
        public DateTime PlanStartDate { get; set; }
        public DateTime PlanEndDate { get; set; }
        public DateTime? ActualStartDate { get; set; }
        public DateTime? ActualEndDate { get; set; }
        public decimal UsedQuantity { get; set; }
        public Dictionary<string, string> ItemRelations { get; set; } = new Dictionary<string, string>();
        public string? Predecessor { get; set; }
        public List<ProgressItemDetailDTO> Details { get; set; } = new List<ProgressItemDetailDTO>();
    }

    public class ProgressItemDetailDTO
    {
        public int Id { get; set; }
        public int ProgressItemId { get; set; }
        public string WorkCode { get; set; } = string.Empty;
        public int ResourceType { get; set; }
        public int Quantity { get; set; }
        public int UsedQuantity { get; set; }
        public string? Unit { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total { get; set; }
        public int? ResourceId { get; set; }
        public ResourceDTO? Resource { get; set; }
    }

    public class ResourceDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Type { get; set; }
    }

    public class ConstructionProgressQuery : BaseQuery
    {
        public int? ProjectId { get; set; }
        public int? PlanId { get; set; }
    }

    public class UpdateProgressDTO
    {
        public int Id { get; set; }
        public int Progress { get; set; }
        public int Status { get; set; }
        public DateTime? ActualStartDate { get; set; }
        public DateTime? ActualEndDate { get; set; }
    }

    public class UpdateProgressItemsDTO
    {
        public int ProgressId { get; set; }
        public List<UpdateProgressDTO> Items { get; set; } = new List<UpdateProgressDTO>();
    }
} 