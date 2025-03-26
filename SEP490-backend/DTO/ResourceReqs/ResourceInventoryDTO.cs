using Sep490_Backend.Infra.Enums;
using System;

namespace Sep490_Backend.DTO.ResourceReqs
{
    /// <summary>
    /// DTO for Resource Inventory data
    /// </summary>
    public class ResourceInventoryDTO
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public ResourceType ResourceType { get; set; }
        public int Quantity { get; set; }
        public string Unit { get; set; }
        public bool Status { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
} 