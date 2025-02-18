using Sep490_Backend.Infra.Enums;

namespace Sep490_Backend.DTO.Contract
{
    public class SaveContractDTO : BaseRequest
    {
        public int Id { get; set; }
        public string ContractCode { get; set; }
        public int ProjectId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int EstimatedDays { get; set; }
        public ContractStatusEnum Status { get; set; }
        public decimal Tax { get; set; }
        public DateTime SignDate { get; set; }
        public string? Attachment { get; set; }
    }
}
