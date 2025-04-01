using Sep490_Backend.Infra.Enums;
using System.ComponentModel.DataAnnotations;

namespace Sep490_Backend.DTO
{
    public class PubSubMessage
    {
        public PubSubEnum PubSubEnum { get; set; }
        
        [Required(ErrorMessage = "Message data is required")]
        public object Data { get; set; } = new();
    }
}
