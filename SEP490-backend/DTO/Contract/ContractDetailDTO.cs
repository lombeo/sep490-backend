using Sep490_Backend.Infra.Entities;

namespace Sep490_Backend.DTO.Contract
{
    public class ContractDetailDTO : CommonEntity
    {
        public string WorkCode { get; set; }
        public string Index { get; set; }
        public int ContractId { get; set; }
        public string? ParentIndex { get; set; }
        public string WorkName { get; set; }
        public string Unit { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
} 