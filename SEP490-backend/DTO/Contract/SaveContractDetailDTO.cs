using Sep490_Backend.Infra.Entities;
using System.ComponentModel.DataAnnotations;

namespace Sep490_Backend.DTO.Contract
{
    public class SaveContractDetailDTO
    {
        public string? WorkCode { get; set; }
        
        [Required(ErrorMessage = "Index is required for proper ordering of construction work")]
        public string Index { get; set; } = string.Empty;
        
        public string? ParentIndex { get; set; }
        
        [Required(ErrorMessage = "Work name is required for construction task identification")]
        public string WorkName { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Unit of measurement is required for construction materials")]
        public string Unit { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Quantity is required for material planning")]
        public decimal Quantity { get; set; }
        
        [Required(ErrorMessage = "Unit price is required for cost estimation")]
        public decimal UnitPrice { get; set; }
        
        [Required(ErrorMessage = "Total is required for budget tracking")]
        public decimal Total { get; set; }
        
        public bool IsDelete { get; set; } = false;
    }
} 