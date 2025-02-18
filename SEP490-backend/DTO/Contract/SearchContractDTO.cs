using Sep490_Backend.Infra.Enums;
using System.Reflection.Metadata.Ecma335;

namespace Sep490_Backend.DTO.Contract
{
    public class SearchContractDTO : BaseQuery
    {
        public string? KeyWord { get; set; }
        public int ProjectId { get; set; }
        public ContractStatusEnum? Status { get; set; }
        public DateTime? SignDate { get; set; }
    }
}
