using Sep490_Backend.DTO;

namespace Sep490_Backend.DTO.ConstructionLog
{
    public class ConstructionLogQueryDTO : BaseQuery
    {
        public int? ProjectId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? SearchTerm { get; set; }
    }
} 