using Sep490_Backend.Infra.Enums;

namespace Sep490_Backend.DTO
{
    public class PubSubMessage
    {
        public PubSubEnum PubSubEnum { get; set; }
        public object Data { get; set; }
    }
}
