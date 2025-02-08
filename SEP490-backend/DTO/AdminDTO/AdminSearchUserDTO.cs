namespace Sep490_Backend.DTO.AdminDTO
{
    public class AdminSearchUserDTO : BaseQueryDTO
    {
        public string? KeyWord { get; set; }
        public string? Role { get; set; }
        public bool? Gender { get; set; }
        public DateTime? Dob { get; set; }
    }
}
