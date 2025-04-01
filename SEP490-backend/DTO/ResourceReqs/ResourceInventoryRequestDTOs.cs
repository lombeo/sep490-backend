using Sep490_Backend.Infra.Enums;
using System.ComponentModel.DataAnnotations;

namespace Sep490_Backend.DTO.ResourceReqs
{
    /// <summary>
    /// DTO for adding a new resource to inventory
    /// </summary>
    public class AddResourceInventoryDTO
    {
        [Required(ErrorMessage = "Resource name is required")]
        public string Name { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Resource description is required")]
        public string Description { get; set; } = string.Empty;
        
        public ResourceType ResourceType { get; set; }
        public int Quantity { get; set; }
        
        [Required(ErrorMessage = "Unit of measurement is required")]
        public string Unit { get; set; } = string.Empty;
        
        public bool Status { get; set; }
    }

    /// <summary>
    /// DTO for updating an existing resource in inventory
    /// </summary>
    public class UpdateResourceInventoryDTO
    {
        public int Id { get; set; }
        
        [Required(ErrorMessage = "Resource name is required")]
        public string Name { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Resource description is required")]
        public string Description { get; set; } = string.Empty;
        
        public ResourceType ResourceType { get; set; }
        public int Quantity { get; set; }
        
        [Required(ErrorMessage = "Unit of measurement is required")]
        public string Unit { get; set; } = string.Empty;
        
        public bool Status { get; set; }
    }
} 