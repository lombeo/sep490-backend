namespace Sep490_Backend.DTO.AuthenDTO
{
    public class ChangePasswordDTO
    {
        public int UserId { get; set; }
        public string? OtpCode { get; set; }
        public string CurrentPassword { get; set; }
        public string NewPassword { get; set; }
        public string ConfirmPassword { get; set; }
    }
}
