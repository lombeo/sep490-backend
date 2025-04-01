using System.ComponentModel.DataAnnotations;

namespace Sep490_Backend.DTO.ResourceReqs
{
    /// <summary>
    /// DTO for approving a request
    /// </summary>
    public class ApproveRequestDTO
    {
        [Required(ErrorMessage = "Comments are required for approval")]
        public string Comments { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for rejecting a request
    /// </summary>
    public class RejectRequestDTO
    {
        [Required(ErrorMessage = "Rejection reason is required")]
        public string Reason { get; set; } = string.Empty;
    }
}