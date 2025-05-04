using System.ComponentModel.DataAnnotations;

namespace Sep490_Backend.DTO.Customer
{
    public class CustomerDTO
    {
        public int Id { get; set; }
        
        [Required(ErrorMessage = "Customer name is required")]
        public string CustomerName { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Director name is required")]
        public string DirectorName { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Phone number is required")]
        [Phone(ErrorMessage = "Invalid phone number format")]
        public string Phone { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Address is required")]
        public string Address { get; set; } = string.Empty;
        
        public string? Description { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Customer code is required")]
        public string CustomerCode { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Tax code is required")]
        public string TaxCode { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Fax number is required")]
        public string Fax { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Bank account is required")]
        public string BankAccount { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Bank name is required")]
        public string BankName { get; set; } = string.Empty;
        
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
