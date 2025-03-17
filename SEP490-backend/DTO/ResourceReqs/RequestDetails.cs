using Sep490_Backend.Infra.Enums;
using System.Text.Json;

namespace Sep490_Backend.DTO.ResourceReqs
{
    public class RequestDetails
    {
        public string? Unit { get; set; }
        public int Quantity { get; set; }
        public JsonDocument Object { get; set; } //Vehicle, User, Material
        public ResourceType ResourceType { get; set; }
        public RequestDetailType Type { get; set; }
    }
}
