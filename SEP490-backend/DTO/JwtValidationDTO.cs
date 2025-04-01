using System.ComponentModel.DataAnnotations;

namespace Sep490_Backend.DTO
{
    public class JwtValidationDTO
    {
        [Required(ErrorMessage = "Valid issuer is required")]
        public string ValidIssuer { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Valid audience is required")]
        public string ValidAudience { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Certificate path is required")]
        public string CertificatePath { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Certificate password is required")]
        public string CertificatePassword { get; set; } = string.Empty;
    }
}
