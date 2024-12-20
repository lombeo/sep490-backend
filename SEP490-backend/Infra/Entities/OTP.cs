using Microsoft.EntityFrameworkCore;
using Sep490_Backend.Infra.Enums;

namespace Sep490_Backend.Infra.Entities
{
    public class OTP
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public ReasonOTP Reason { get; set; }
        public string Code { get; set; }
        public DateTime ExpiryTime { get; set; }
    }
}
