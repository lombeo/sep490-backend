using System.Text.Json;

namespace Sep490_Backend.DTO.ConstructionLog
{
    public class ConstructionLogDTO
    {
        public int Id { get; set; }
        public string LogCode { get; set; } = string.Empty;
        public string LogName { get; set; } = string.Empty;
        public DateTime LogDate { get; set; }
        public int ProjectId { get; set; }
        public List<LogResourceDTO> Resources { get; set; } = new List<LogResourceDTO>();
        public List<LogWorkAmountDTO> WorkAmount { get; set; } = new List<LogWorkAmountDTO>();
        public List<WeatherDTO> Weather { get; set; } = new List<WeatherDTO>();
        public int? Safety { get; set; }
        public int? Quality { get; set; }
        public int? Progress { get; set; }
        public string? Problem { get; set; }
        public string? Advice { get; set; }
        public JsonDocument? Images { get; set; }
        public JsonDocument? Attachments { get; set; }
        public string? Note { get; set; }
    }
} 