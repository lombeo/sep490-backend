using System.ComponentModel.DataAnnotations;

namespace Sep490_Backend.DTO.Customer
{
    public class CustomerUpdateDTO
    {
        [Required(ErrorMessage = "Customer ID is required for update")]
        public int Id { get; set; }
        
        [Required(ErrorMessage = "Customer code is required for client identification")]
        public string CustomerCode { get; set; } = string.Empty;
        
        public string? CustomerName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        
        [Required(ErrorMessage = "Tax code is required for construction client registration")]
        public string TaxCode { get; set; } = string.Empty;
        
        public string? Fax { get; set; }
        public string? Address { get; set; }
        public string? Email { get; set; }
        public string? DirectorName { get; set; }
        public string? Description { get; set; }
        public string? BankAccount { get; set; }
        public string? BankName { get; set; }
    }
}
