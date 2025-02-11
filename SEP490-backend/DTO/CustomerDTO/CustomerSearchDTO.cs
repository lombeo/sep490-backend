using MimeKit.Tnef;

namespace Sep490_Backend.DTO.CustomerDTO
{
    public class CustomerSearchDTO : BaseQueryDTO
    {
        public string? Search { get; set; }
    }
}
