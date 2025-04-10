using Sep490_Backend.Infra.Enums;

namespace Sep490_Backend.DTO.ConstructionLog
{
    public class LogResourceDTO
    {
        public int Id { get; set; }
        public int ResourceType { get; set; }
        public int TaskIndex { get; set; }
        public int Quantity { get; set; }
        public string ResourceId { get; set; } = string.Empty;
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
    }
} 