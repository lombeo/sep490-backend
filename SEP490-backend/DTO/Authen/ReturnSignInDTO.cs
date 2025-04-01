using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace Sep490_Backend.DTO.Authen
{
    public class ReturnSignInDTO
    {
        public int UserId { get; set; }
        
        [Required(ErrorMessage = "Role is required for access control")]
        public string Role { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Username is required")]
        public string Username { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Access token is required for authentication")]
        public string AccessToken { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Refresh token is required for session management")]
        public string RefreshToken { get; set; } = string.Empty;
        
        public bool IsVerify { get; set; }
    }
}
