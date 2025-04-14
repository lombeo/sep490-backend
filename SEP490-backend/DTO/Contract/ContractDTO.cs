using Sep490_Backend.DTO.Project;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Infra.Enums;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Sep490_Backend.DTO.Contract
{
    public class ContractDTO : CommonEntity
    {
        public int Id { get; set; }
        
        [Required(ErrorMessage = "Contract code is required for construction project tracking")]
        public string ContractCode { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Contract name is required for project identification")]
        public string ContractName { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Project information is required")]
        public ProjectDTO Project { get; set; } = new ProjectDTO();
        
        public int ProjectId { get; set; }
        
        public string? ContractNumber { get; set; }
        
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int EstimatedDays { get; set; }
        public ContractStatusEnum Status { get; set; }
        public decimal Tax { get; set; }
        public DateTime SignDate { get; set; }
        public DateTime SignedDate { get; set; }
        public DateTime CompletionDate { get; set; }
        public decimal Value { get; set; }
        public string? CustomerRepName { get; set; }
        public string? CustomerRepTitle { get; set; }
        public string? CompanyRepName { get; set; }
        public string? CompanyRepTitle { get; set; }
        public string? Description { get; set; }
        public List<AttachmentInfo>? Attachments { get; set; }
        public List<ContractDetailDTO> ContractDetails { get; set; } = new List<ContractDetailDTO>();
    }
}
