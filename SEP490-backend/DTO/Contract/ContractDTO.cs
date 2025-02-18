using Sep490_Backend.DTO.Project;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Infra.Enums;

namespace Sep490_Backend.DTO.Contract
{
    public class ContractDTO : CommonEntity
    {
        public int Id { get; set; }
        public string ContractCode { get; set; }
        public ProjectDTO Project { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int EstimatedDays { get; set; }
        public ContractStatusEnum Status { get; set; }
        public decimal Tax { get; set; }
        public DateTime SignDate { get; set; }
        public string? Attachment { get; set; }
    }
}
