namespace Sep490_Backend.DTO.CustomerDTO
{
    public class CustomerCreateDTO
    {
        public string CustomerCode { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? TaxCode { get; set; }
        public string? Fax { get; set; }
        public string? Address { get; set; }
        public string? Email { get; set; }
        public string? DirectorName { get; set; }
        public string? Description { get; set; }
        public string? BankAccount { get; set; }
        public string? BankName { get; set; }
    }
}
