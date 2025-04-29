using Sep490_Backend.Infra.Enums;
using System.ComponentModel.DataAnnotations;

namespace Sep490_Backend.DTO.ConstructionProgress
{
    public class UpdateProgressItemDTO
    {
        [Required(ErrorMessage = "Progress item ID is required")]
        public int Id { get; set; }
        
        [StringLength(50, ErrorMessage = "Work code cannot exceed 50 characters")]
        public string? WorkCode { get; set; }
        
        [StringLength(50, ErrorMessage = "Index cannot exceed 50 characters")]
        public string? Index { get; set; }
        
        [StringLength(50, ErrorMessage = "Parent index cannot exceed 50 characters")]
        public string? ParentIndex { get; set; }
        
        [StringLength(200, ErrorMessage = "Work name cannot exceed 200 characters")]
        public string? WorkName { get; set; }
        
        [StringLength(50, ErrorMessage = "Unit cannot exceed 50 characters")]
        public string? Unit { get; set; }
        
        [Range(0.01, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
        public decimal? Quantity { get; set; }
        
        [Range(0, double.MaxValue, ErrorMessage = "Unit price must be non-negative")]
        public decimal? UnitPrice { get; set; }
        
        [Range(0, double.MaxValue, ErrorMessage = "Total price must be non-negative")]
        public decimal? TotalPrice { get; set; }
        
        [Range(0, 100, ErrorMessage = "Progress must be between 0 and 100")]
        public int? Progress { get; set; }
        
        public ProgressStatusEnum? Status { get; set; }
        
        public DateTime? PlanStartDate { get; set; }
        
        public DateTime? PlanEndDate { get; set; }
        
        public DateTime? ActualStartDate { get; set; }
        
        public DateTime? ActualEndDate { get; set; }
        
        public decimal? UsedQuantity { get; set; }
        
        public Dictionary<string, string>? ItemRelations { get; set; }
        
        public List<UpdateProgressItemDetailDTO>? Details { get; set; }
    }

    public class UpdateProgressItemDetailDTO
    {
        public int? Id { get; set; }
        
        [StringLength(50, ErrorMessage = "Work code cannot exceed 50 characters")]
        public string? WorkCode { get; set; }
        
        public ResourceType? ResourceType { get; set; }
        
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
        public int? Quantity { get; set; }
        
        public int? UsedQuantity { get; set; }
        
        [StringLength(50, ErrorMessage = "Unit cannot exceed 50 characters")]
        public string? Unit { get; set; }
        
        [Range(0, double.MaxValue, ErrorMessage = "Unit price must be non-negative")]
        public decimal? UnitPrice { get; set; }
        
        [Range(0, double.MaxValue, ErrorMessage = "Total must be non-negative")]
        public decimal? Total { get; set; }
        
        public int? ResourceId { get; set; }
    }
} 