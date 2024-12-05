namespace Sep490_Backend.DTO
{
    public class RedisConfigDTO
    {
        public string ConnectionString { get; set; }
        public string PubSubConnection { get; set; }
        public string PubSubChannel { get; set; }
    }
}
