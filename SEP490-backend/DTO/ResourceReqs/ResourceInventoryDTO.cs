using Sep490_Backend.Infra.Enums;
using System;
using System.ComponentModel.DataAnnotations;

namespace Sep490_Backend.DTO.ResourceReqs
{
    /// <summary>
    /// DTO for Resource Inventory data
    /// </summary>
    public class ResourceInventoryDTO
    {
        public int Id { get; set; }
        
        [Required(ErrorMessage = "Resource name is required")]
        public string Name { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Resource description is required")]
        public string Description { get; set; } = string.Empty;
        
        public int? ResourceId { get; set; }
        public int? ProjectId { get; set; }
        public ResourceType ResourceType { get; set; }
        public int Quantity { get; set; }
        
        [Required(ErrorMessage = "Unit of measurement is required")]
        public string Unit { get; set; } = string.Empty;
        
        public bool Status { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
} 