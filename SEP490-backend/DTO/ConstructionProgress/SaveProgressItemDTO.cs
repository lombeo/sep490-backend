using Sep490_Backend.Infra.Enums;
using System.ComponentModel.DataAnnotations;

namespace Sep490_Backend.DTO.ConstructionProgress
{
    public class SaveProgressItemDTO
    {
        [Required]
        public int ProgressId { get; set; }
        
        [Required]
        public string Index { get; set; } = string.Empty;
        
        public string? ParentIndex { get; set; }
        
        [Required]
        public string WorkName { get; set; } = string.Empty;
        
        [Required]
        public string Unit { get; set; } = string.Empty;
        
        [Required]
        public decimal Quantity { get; set; }
        
        public decimal UnitPrice { get; set; }
        
        public decimal TotalPrice { get; set; }
        
        [Range(0, 100)]
        public int Progress { get; set; } = 0;
        
        public int Status { get; set; } = 0;
        
        [Required]
        public DateTime PlanStartDate { get; set; }
        
        [Required]
        public DateTime PlanEndDate { get; set; }
        
        public DateTime? ActualStartDate { get; set; }
        
        public DateTime? ActualEndDate { get; set; }
        
        public Dictionary<string, string> ItemRelations { get; set; } = new Dictionary<string, string>();
    }
} 