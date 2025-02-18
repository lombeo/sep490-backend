using Newtonsoft.Json;

namespace Sep490_Backend.DTO.Authen
{
    public class ReturnSignInDTO
    {
        public string Role { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public bool IsVerify {  get; set; }
    }
}
