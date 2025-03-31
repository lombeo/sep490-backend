using Newtonsoft.Json;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Infra.Helps;
using System.Text.Json.Serialization;
using JsonConverter = System.Text.Json.Serialization.JsonConverterAttribute;

namespace Sep490_Backend.DTO.ConstructionPlan
{
    public class ConstructionPlanDTO
    {
        public int Id { get; set; }
        public string PlanName { get; set; }
        
        [JsonConverter(typeof(ReviewerDictionaryConverter))]
        public Dictionary<int, bool>? Reviewer { get; set; }
        public int ProjectId { get; set; }
        public string ProjectName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int CreatedBy { get; set; }
        public string CreatedByName { get; set; }
        public int? UpdatedBy { get; set; }
        public List<ConstructPlanItemDTO> PlanItems { get; set; } = new List<ConstructPlanItemDTO>();
        public List<ReviewerDTO> Reviewers { get; set; } = new List<ReviewerDTO>();
        public bool IsApproved { get; set; }
    }

    public class ConstructPlanItemDTO
    {
        public string WorkCode { get; set; }
        public string Index { get; set; }
        public int PlanId { get; set; }
        public string? ParentIndex { get; set; }
        public string WorkName { get; set; }
        public string Unit { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public Dictionary<string, string> ItemRelations { get; set; }
        public List<ConstructPlanItemDetailDTO> Details { get; set; } = new List<ConstructPlanItemDetailDTO>();
        public List<ConstructionTeamDTO> Teams { get; set; } = new List<ConstructionTeamDTO>();
        public List<QAMemberDTO> QAMembers { get; set; } = new List<QAMemberDTO>();
    }

    public class ConstructPlanItemDetailDTO
    {
        public int Id { get; set; }
        public string PlanItemId { get; set; }
        public string WorkCode { get; set; }
        public string ResourceType { get; set; }
        public int Quantity { get; set; }
        public string Unit { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total { get; set; }
        public int? ResourceId { get; set; }
        public ResourceDTO Resource { get; set; }
    }

    public class QAMemberDTO
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
    }

    public class ResourceDTO
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
    }

    public class ReviewerDTO
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public bool IsApproved { get; set; }
    }

    public class ConstructionTeamDTO
    {
        public int Id { get; set; }
        public string TeamName { get; set; }
        public int TeamManager { get; set; }
        public string TeamManagerName { get; set; }
        public string Description { get; set; }
    }
} 