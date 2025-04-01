using System.ComponentModel.DataAnnotations;

namespace Sep490_Backend.DTO
{
    public class UserDTO
    {
        public int UserId { get; set; }
        
        [Required(ErrorMessage = "Username is required")]
        public string Username { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Role is required")]
        public string Role { get; set; } = string.Empty;
        
        public bool IsVerify { get; set; }
        
        [Required(ErrorMessage = "Full name is required")]
        public string FullName { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Phone number is required")]
        [Phone(ErrorMessage = "Invalid phone number format")]
        public string Phone { get; set; } = string.Empty;
        
        public bool? Gender { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? CreatedAt { get; set; }
        public int Creator { get; set; }
        public int Updater { get; set; }
        public string? PicProfile { get; set; }
        public string? Address { get; set; }
        public DateTime? Dob { get; set; }
    }
}
