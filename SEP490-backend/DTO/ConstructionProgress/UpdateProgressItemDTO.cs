using Sep490_Backend.Infra.Enums;
using System.ComponentModel.DataAnnotations;

namespace Sep490_Backend.DTO.ConstructionProgress
{
    public class UpdateProgressItemDTO
    {
        [Required]
        public int ProgressId { get; set; }
        
        [Required]
        public int Id { get; set; }
        
        [Range(0, 100)]
        public int Progress { get; set; }
        
        public int Status { get; set; }
        
        public DateTime? ActualStartDate { get; set; }
        
        public DateTime? ActualEndDate { get; set; }
        
        public DateTime PlanStartDate { get; set; }
        
        public DateTime PlanEndDate { get; set; }
        
        public Dictionary<string, string>? ItemRelations { get; set; }
        
        // Optional quantity field for updating progress item quantity
        [Range(0, int.MaxValue)]
        public int? Quantity { get; set; }
    }
} 