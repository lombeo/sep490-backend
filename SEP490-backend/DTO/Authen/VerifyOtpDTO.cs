using Sep490_Backend.Infra.Enums;
using System.ComponentModel.DataAnnotations;

namespace Sep490_Backend.DTO.Authen
{
    public class VerifyOtpDTO
    {
        public int UserId { get; set; }
        
        [Required(ErrorMessage = "OTP code is required for verification")]
        public string OtpCode { get; set; } = string.Empty;
        
        public ReasonOTP Reason { get; set; }
    }
}
