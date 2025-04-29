using Sep490_Backend.Infra.Enums;
using System.ComponentModel.DataAnnotations;

namespace Sep490_Backend.DTO.ConstructionProgress
{
    public class CreateProgressItemDTO
    {
        [Required(ErrorMessage = "Progress ID is required")]
        public int ProgressId { get; set; }
        
        [Required(ErrorMessage = "Work code is required")]
        [StringLength(50, ErrorMessage = "Work code cannot exceed 50 characters")]
        public string WorkCode { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Index is required")]
        [StringLength(50, ErrorMessage = "Index cannot exceed 50 characters")]
        public string Index { get; set; } = string.Empty;
        
        [StringLength(50, ErrorMessage = "Parent index cannot exceed 50 characters")]
        public string? ParentIndex { get; set; }
        
        [Required(ErrorMessage = "Work name is required")]
        [StringLength(200, ErrorMessage = "Work name cannot exceed 200 characters")]
        public string WorkName { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Unit is required")]
        [StringLength(50, ErrorMessage = "Unit cannot exceed 50 characters")]
        public string Unit { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Quantity is required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
        public decimal Quantity { get; set; }
        
        [Required(ErrorMessage = "Unit price is required")]
        [Range(0, double.MaxValue, ErrorMessage = "Unit price must be non-negative")]
        public decimal UnitPrice { get; set; }
        
        [Required(ErrorMessage = "Total price is required")]
        [Range(0, double.MaxValue, ErrorMessage = "Total price must be non-negative")]
        public decimal TotalPrice { get; set; }
        
        [Range(0, 100, ErrorMessage = "Progress must be between 0 and 100")]
        public int Progress { get; set; } = 0;
        
        public ProgressStatusEnum Status { get; set; } = ProgressStatusEnum.NotStarted;
        
        [Required(ErrorMessage = "Plan start date is required")]
        public DateTime PlanStartDate { get; set; }
        
        [Required(ErrorMessage = "Plan end date is required")]
        public DateTime PlanEndDate { get; set; }
        
        public DateTime? ActualStartDate { get; set; }
        
        public DateTime? ActualEndDate { get; set; }
        
        public decimal UsedQuantity { get; set; } = 0;
        
        public Dictionary<string, string> ItemRelations { get; set; } = new Dictionary<string, string>();
        
        public List<CreateProgressItemDetailDTO> Details { get; set; } = new List<CreateProgressItemDetailDTO>();
    }

    public class CreateProgressItemDetailDTO
    {
        [Required(ErrorMessage = "Work code is required")]
        [StringLength(50, ErrorMessage = "Work code cannot exceed 50 characters")]
        public string WorkCode { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Resource type is required")]
        public ResourceType ResourceType { get; set; }
        
        [Required(ErrorMessage = "Quantity is required")]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
        public int Quantity { get; set; }
        
        public int UsedQuantity { get; set; } = 0;
        
        [StringLength(50, ErrorMessage = "Unit cannot exceed 50 characters")]
        public string? Unit { get; set; }
        
        [Required(ErrorMessage = "Unit price is required")]
        [Range(0, double.MaxValue, ErrorMessage = "Unit price must be non-negative")]
        public decimal UnitPrice { get; set; }
        
        [Required(ErrorMessage = "Total is required")]
        [Range(0, double.MaxValue, ErrorMessage = "Total must be non-negative")]
        public decimal Total { get; set; }
        
        public int? ResourceId { get; set; }
    }
} 