namespace Sep490_Backend.DTO.Authen
{
    public class ChangePasswordDTO
    {
        public string? CurrentPassword { get; set; }
        public string? NewPassword { get; set; }
        public string? ConfirmPassword { get; set; }
    }
}
