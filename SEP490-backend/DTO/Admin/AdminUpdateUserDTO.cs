namespace Sep490_Backend.DTO.Admin
{
    public class AdminUpdateUserDTO
    {
        public int Id { get; set; } 
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string? Role { get; set; }
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public bool? Gender { get; set; }
        public bool? IsVerify { get; set; }
        public DateTime? Dob { get; set; }
        public int? TeamId { get; set; }
    }
}
