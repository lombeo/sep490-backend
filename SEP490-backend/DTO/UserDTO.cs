namespace Sep490_Backend.DTO
{
    public class UserDTO
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public bool IsVerify { get; set; }
        public string FullName { get; set; }
        public string Phone { get; set; }
        public bool? Gender { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
