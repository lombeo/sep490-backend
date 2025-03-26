using Sep490_Backend.Infra.Enums;

namespace Sep490_Backend.DTO.ResourceReqs
{
    /// <summary>
    /// DTO for adding a new resource to inventory
    /// </summary>
    public class AddResourceInventoryDTO
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public ResourceType ResourceType { get; set; }
        public int Quantity { get; set; }
        public string Unit { get; set; }
        public bool Status { get; set; }
    }

    /// <summary>
    /// DTO for updating an existing resource in inventory
    /// </summary>
    public class UpdateResourceInventoryDTO
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public ResourceType ResourceType { get; set; }
        public int Quantity { get; set; }
        public string Unit { get; set; }
        public bool Status { get; set; }
    }
} 