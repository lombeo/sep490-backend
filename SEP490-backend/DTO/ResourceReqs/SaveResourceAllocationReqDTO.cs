using Sep490_Backend.DTO.ResourceReqs;
using Sep490_Backend.Infra.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Sep490_Backend.DTO.ResourceReqs
{
    public class SaveResourceAllocationReqDTO
    {
        public int? Id { get; set; }
        public string? RequestCode { get; set; }
        public int RequestType { get; set; } = 1; // Default to PROJECT_TO_PROJECT
        public int FromProjectId { get; set; }
        public int ToProjectId { get; set; }
        public int? FromTaskId { get; set; }
        public int? ToTaskId { get; set; }
        public string? RequestName { get; set; }
        
        [Required(ErrorMessage = "Resource allocation details are required")]
        public List<RequestDetails> ResourceAllocationDetails { get; set; } = new List<RequestDetails>();
        
        public string? Description { get; set; }
        public PriorityLevel PriorityLevel { get; set; }
        public RequestStatus Status { get; set; }
        public JsonDocument? Attachments { get; set; }
        public DateTime RequestDate { get; set; }
    }
} 