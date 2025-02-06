namespace Sep490_Backend.DTO.AdminDTO
{
    public class AdminCreateUserDTO
    {
        public string UserName { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public string FullName { get; set; }
        public string Phone { get; set; }
        public bool Gender { get; set; }
        public DateTime Dob { get; set; }
        public bool IsVerify { get; set; }
    }
}
