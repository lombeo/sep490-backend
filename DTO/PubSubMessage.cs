using Api_Project_Prn.Infra.Enums;

namespace Api_Project_Prn.DTO
{
    public class PubSubMessage
    {
        public PubSubEnum PubSubEnum { get; set; }
        public object Data { get; set; }
    }
}
