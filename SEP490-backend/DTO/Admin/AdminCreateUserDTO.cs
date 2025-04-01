using System;
using System.ComponentModel.DataAnnotations;

namespace Sep490_Backend.DTO.Admin
{
    public class AdminCreateUserDTO
    {
        [Required(ErrorMessage = "Username is required")]
        public string UserName { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Email is required")]
        public string Email { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Role is required")]
        public string Role { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Full name is required")]
        public string FullName { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Phone number is required")]
        public string Phone { get; set; } = string.Empty;
        
        public bool Gender { get; set; }
        public DateTime Dob { get; set; }
        public bool IsVerify { get; set; }
    }
}
