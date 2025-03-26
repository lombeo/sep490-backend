namespace Sep490_Backend.DTO.ResourceReqs
{
    /// <summary>
    /// DTO for approving a request
    /// </summary>
    public class ApproveRequestDTO
    {
        public string Comments { get; set; }
    }

    /// <summary>
    /// DTO for rejecting a request
    /// </summary>
    public class RejectRequestDTO
    {
        public string Reason { get; set; }
    }
} 