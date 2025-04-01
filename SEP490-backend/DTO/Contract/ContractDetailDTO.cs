using Sep490_Backend.Infra.Entities;
using System.ComponentModel.DataAnnotations;

namespace Sep490_Backend.DTO.Contract
{
    public class ContractDetailDTO : CommonEntity
    {
        [Required(ErrorMessage = "Work code is required for construction management")]
        public string WorkCode { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Index is required for proper work ordering")]
        public string Index { get; set; } = string.Empty;
        
        public int ContractId { get; set; }
        
        public string? ParentIndex { get; set; }
        
        [Required(ErrorMessage = "Work name is required to identify the construction task")]
        public string WorkName { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Unit of measurement is required for construction materials")]
        public string Unit { get; set; } = string.Empty;
        
        [Required]
        public decimal Quantity { get; set; }
        
        [Required]
        public decimal UnitPrice { get; set; }
        
        [Required]
        public decimal Total { get; set; }
    }
} 