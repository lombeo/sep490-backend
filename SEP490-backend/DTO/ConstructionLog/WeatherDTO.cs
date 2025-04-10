namespace Sep490_Backend.DTO.ConstructionLog
{
    public class WeatherDTO
    {
        public string Type { get; set; } = string.Empty;
        public List<string> Values { get; set; } = new List<string>();
    }
} 