using Sep490_Backend.DTO.ResourceReqs;
using Sep490_Backend.Infra.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Sep490_Backend.DTO.ResourceReqs
{
    public class SaveResourceMobilizationReqDTO
    {
        public int? Id { get; set; }
        public string? RequestCode { get; set; }
        public int ProjectId { get; set; }
        public string? RequestName { get; set; }
        
        [Required(ErrorMessage = "Resource mobilization details are required")]
        public List<RequestDetails> ResourceMobilizationDetails { get; set; } = new List<RequestDetails>();
        
        public string? Description { get; set; }
        public PriorityLevel PriorityLevel { get; set; }
        public RequestStatus Status { get; set; }
        public JsonDocument? Attachments { get; set; }
        public DateTime RequestDate { get; set; }
        public MobilizationRequestType RequestType { get; set; } = MobilizationRequestType.SupplyMore;
    }
} 