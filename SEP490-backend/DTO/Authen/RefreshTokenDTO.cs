using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;

namespace Sep490_Backend.DTO.Authen
{
    public class RefreshTokenDTO
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        
        [Required(ErrorMessage = "Token is required for authentication renewal")]
        public string Token { get; set; } = string.Empty;
        
        public DateTime Expires { get; set; }
        public DateTime Created { get; set; } = DateTime.UtcNow;
        public DateTime? Revoked { get; set; } // Đã thu hồi
        public bool IsExpired => DateTime.UtcNow >= Expires;
        public bool IsRevoked => Revoked != null;
        public bool IsActive => !IsExpired && !IsRevoked;
    }
}
