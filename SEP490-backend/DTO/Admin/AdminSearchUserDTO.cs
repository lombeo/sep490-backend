namespace Sep490_Backend.DTO.Admin
{
    public class AdminSearchUserDTO : BaseQuery
    {
        public string? KeyWord { get; set; }
        public string? Role { get; set; }
        public bool? Gender { get; set; }
        public DateTime? Dob { get; set; }
    }
}
