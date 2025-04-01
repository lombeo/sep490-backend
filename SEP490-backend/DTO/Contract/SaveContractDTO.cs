using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Sep490_Backend.Infra.Enums;
using Sep490_Backend.Infra.ModelBinders;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Sep490_Backend.DTO.Contract
{
    public class SaveContractDTO : BaseRequest
    {
        public int Id { get; set; }
        
        [Required(ErrorMessage = "Contract code is required")]
        public string ContractCode { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Contract name is required")]
        public string ContractName { get; set; } = string.Empty;
        
        public int ProjectId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int EstimatedDays { get; set; }
        public ContractStatusEnum Status { get; set; }
        public decimal Tax { get; set; }
        public DateTime SignDate { get; set; }
        
        public new int ActionBy { get; set; }
        public List<IFormFile>? Attachments { get; set; }
        
        [ModelBinder(BinderType = typeof(ContractDetailModelBinder))]
        public List<SaveContractDetailDTO> ContractDetails { get; set; } = new List<SaveContractDetailDTO>();
    }

    public class ContractDetailSave
    {
        [Required(ErrorMessage = "Work code is required")]
        public string WorkCode { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Index is required")]
        public string Index { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Parent index is required")]
        public string ParentIndex { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Work name is required")]
        public string WorkName { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Unit is required")]
        public string Unit { get; set; } = string.Empty;
        
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public bool IsDelete { get; set; }
    }
}
