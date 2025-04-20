using Newtonsoft.Json;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Infra.Helps;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using JsonConverter = System.Text.Json.Serialization.JsonConverterAttribute;

namespace Sep490_Backend.DTO.ConstructionPlan
{
    public class ConstructionPlanDTO
    {
        public int Id { get; set; }
        
        [Required(ErrorMessage = "Construction plan name is required")]
        public string PlanName { get; set; } = string.Empty;
        
        public int ProjectId { get; set; }
        
        [Required(ErrorMessage = "Project name is required for construction management")]
        public string ProjectName { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int CreatedBy { get; set; }
        
        [Required(ErrorMessage = "Creator name is required for audit trail")]
        public string CreatedByName { get; set; } = string.Empty;
        
        public int? UpdatedBy { get; set; }
        public List<ConstructPlanItemDTO> PlanItems { get; set; } = new List<ConstructPlanItemDTO>();
        public List<ReviewerDTO> Reviewers { get; set; } = new List<ReviewerDTO>();
        public bool IsApproved { get; set; }
    }

    public class ConstructPlanItemDTO
    {
        public int Id { get; set; }
        
        [Required(ErrorMessage = "Work code is required for construction item tracking")]
        public string WorkCode { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Index is required for proper ordering of construction tasks")]
        public string Index { get; set; } = string.Empty;
        
        public int PlanId { get; set; }
        public string? ParentIndex { get; set; }
        
        [Required(ErrorMessage = "Work name is required for site leveling activities")]
        public string WorkName { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Unit of measurement is required for construction materials")]
        public string Unit { get; set; } = string.Empty;
        
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        
        [Required(ErrorMessage = "Item relations are required for construction planning")]
        public Dictionary<string, string> ItemRelations { get; set; } = new Dictionary<string, string>();
        
        public List<ConstructPlanItemDetailDTO> Details { get; set; } = new List<ConstructPlanItemDetailDTO>();
        public List<ConstructionTeamDTO> Teams { get; set; } = new List<ConstructionTeamDTO>();
    }

    public class ConstructPlanItemDetailDTO
    {
        public int Id { get; set; }
        
        [Required(ErrorMessage = "Plan item ID is required for resource allocation")]
        public int PlanItemId { get; set; }
        
        [Required(ErrorMessage = "Work code is required for construction activity")]
        public string WorkCode { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Resource type is required for construction management")]
        public string ResourceType { get; set; } = string.Empty;
        
        public int Quantity { get; set; }
        
        [Required(ErrorMessage = "Unit of measurement is required")]
        public string Unit { get; set; } = string.Empty;
        
        public decimal UnitPrice { get; set; }
        public decimal Total { get; set; }
        public int? ResourceId { get; set; }
        
        [Required(ErrorMessage = "Resource information is required")]
        public ResourceDTO Resource { get; set; } = new ResourceDTO();
    }

    public class ResourceDTO
    {
        public int Id { get; set; }
        
        [Required(ErrorMessage = "Resource name is required")]
        public string Name { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Resource type is required")]
        public int Type { get; set; }
    }

    public class ReviewerDTO
    {
        public int Id { get; set; }
        
        [Required(ErrorMessage = "Reviewer name is required")]
        public string Name { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Reviewer email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = string.Empty;
        
        public bool? IsApproved { get; set; }
        
        [Required(ErrorMessage = "Reviewer role is required")]
        public string Role { get; set; } = string.Empty;
    }

    public class ConstructionTeamDTO
    {
        public int Id { get; set; }
        
        [Required(ErrorMessage = "Team name is required for construction crew management")]
        public string TeamName { get; set; } = string.Empty;
        
        public int TeamManager { get; set; }
        
        [Required(ErrorMessage = "Team manager name is required")]
        public string TeamManagerName { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Team description is required")]
        public string Description { get; set; } = string.Empty;
    }
} 