using Microsoft.AspNetCore.Http;

namespace Sep490_Backend.DTO.Authen
{
    public class UserUpdateProfileDTO
    {
        public string? Username { get; set; }
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public bool? Gender { get; set; }
        public DateTime? Dob { get; set; }
        public string? Address { get; set; }
        public IFormFile? PicProfile { get; set; }
    }
}
