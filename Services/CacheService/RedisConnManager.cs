using Api_Project_Prn.Infra.Constants;
using StackExchange.Redis;

namespace Api_Project_Prn.Services.CacheService
{
    public class RedisConnManager
    {
        public RedisConnManager()
        {
            lock (locker)
            {
                if (lazyConnection == null)
                {
                    lazyConnection = new Lazy<ConnectionMultiplexer>(() =>
                    {
                        return ConnectionMultiplexer.Connect(StaticVariable.RedisConfig.PubSubConnection);
                    });
                }
            }
        }
        private static Lazy<ConnectionMultiplexer> lazyConnection;
        private static readonly object locker = new object();
        public ConnectionMultiplexer Connection { get { return lazyConnection.Value; } }
    }
}
