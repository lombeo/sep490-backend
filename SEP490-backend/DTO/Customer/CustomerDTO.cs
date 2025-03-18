namespace Sep490_Backend.DTO.Customer
{
    public class CustomerDTO
    {
        public int Id { get; set; }
        public string CustomerName { get; set; }
        public string DirectorName { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
        public string Description { get; set; }
        public string CustomerCode { get; set; }
        public string TaxCode { get; set; }
        public string Fax { get; set; }
        public string BankAccount { get; set; }
        public string BankName { get; set; }
        public DateTime? CreatedAt { get; set; }
        public int Creator { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int Updater { get; set; }
        public bool Deleted { get; set; }

        public static CustomerDTO FromCustomer(Infra.Entities.Customer customer)
        {
            if (customer == null)
                return new CustomerDTO();

            return new CustomerDTO
            {
                Id = customer.Id,
                CustomerName = customer.CustomerName,
                DirectorName = customer.DirectorName,
                Phone = customer.Phone,
                Email = customer.Email,
                Address = customer.Address,
                Description = customer.Description,
                CustomerCode = customer.CustomerCode,
                TaxCode = customer.TaxCode,
                Fax = customer.Fax,
                BankAccount = customer.BankAccount,
                BankName = customer.BankName,
                CreatedAt = customer.CreatedAt,
                Creator = customer.Creator,
                UpdatedAt = customer.UpdatedAt,
                Updater = customer.Updater,
                Deleted = customer.Deleted
            };
        }
    }
}
