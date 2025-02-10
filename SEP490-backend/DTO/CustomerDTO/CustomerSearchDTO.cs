using MimeKit.Tnef;

namespace Sep490_Backend.DTO.CustomerDTO
{
    public class CustomerSearchDTO : BaseQueryDTO
    {
        public string CustomerCode { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string? Phone { get; set; }
    }
}
