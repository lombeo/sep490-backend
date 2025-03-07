using Sep490_Backend.DTO.ResourceReqs;
using Sep490_Backend.Infra.Enums;
using System.Text.Json;

namespace Sep490_Backend.Infra.Entities
{
    public class ResourceMobilizationReqs : CommonEntity
    {
        public int Id { get; set; }
        public string RequestCode { get; set; } //unique
        public int ProjectId { get; set; }
        public string? RequestName { get; set; }
        public List<RequestDetails> ResourceMobilizationDetails { get; set; } //jsonb
        public string? Description { get; set; }
        public PriorityLevel PriorityLevel { get; set; }
        public RequestStatus Status { get; set; }
        public JsonDocument? Attachments { get; set; }
        public DateTime RequestDate { get; set; }
    }
}
