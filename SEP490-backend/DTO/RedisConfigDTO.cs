using System.ComponentModel.DataAnnotations;

namespace Sep490_Backend.DTO
{
    public class RedisConfigDTO
    {
        [Required(ErrorMessage = "Redis connection string is required")]
        public string ConnectionString { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Redis pubsub connection is required")]
        public string PubSubConnection { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Redis pubsub channel is required")]
        public string PubSubChannel { get; set; } = string.Empty;
    }
}
