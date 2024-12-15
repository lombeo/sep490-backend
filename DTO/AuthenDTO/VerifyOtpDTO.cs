using Sep490_Backend.Infra.Enums;

namespace Sep490_Backend.DTO.AuthenDTO
{
    public class VerifyOtpDTO
    {
        public int UserId { get; set; }
        public string OtpCode { get; set; }
        public ReasonOTP Reason { get; set; }
    }
}
