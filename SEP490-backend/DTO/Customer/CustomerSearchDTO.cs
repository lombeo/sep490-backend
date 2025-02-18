using MimeKit.Tnef;

namespace Sep490_Backend.DTO.Customer
{
    public class CustomerSearchDTO : BaseQuery
    {
        public string? Search { get; set; }
    }
}
