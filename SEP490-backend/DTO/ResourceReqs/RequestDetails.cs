using Sep490_Backend.Infra.Enums;
using System.Text.Json;

namespace Sep490_Backend.DTO.ResourceReqs
{
    public class RequestDetails
    {
        public string? Unit { get; set; }
        public int Quantity { get; set; }
        public int ResourceId { get; set; } //Vehicle, User, Material ID
        public ResourceType ResourceType { get; set; }
        public RequestDetailType Type { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
    }
}
