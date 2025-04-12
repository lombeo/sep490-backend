using Sep490_Backend.Infra.Constants;
using StackExchange.Redis;
using Microsoft.Extensions.Logging;

namespace Sep490_Backend.Services.CacheService
{
    public class RedisConnManager
    {
        private readonly ILogger<RedisConnManager> _logger;
        private static Lazy<ConnectionMultiplexer> lazyConnection;
        private static readonly object locker = new object();

        public RedisConnManager(ILogger<RedisConnManager> logger)
        {
            _logger = logger;
            
            lock (locker)
            {
                if (lazyConnection == null)
                {
                    try
                    {
                        lazyConnection = new Lazy<ConnectionMultiplexer>(() =>
                        {
                            var options = ConfigurationOptions.Parse(StaticVariable.RedisConfig.PubSubConnection);
                            options.ConnectTimeout = 5000; // 5 seconds
                            options.SyncTimeout = 10000; // 10 seconds
                            options.AbortOnConnectFail = false; // Don't crash the app if Redis is down
                            
                            _logger.LogInformation("Connecting to Redis at " + options.EndPoints.First());
                            return ConnectionMultiplexer.Connect(options);
                        });
                        
                        // Test the connection
                        var connection = lazyConnection.Value;
                        var pingResult = connection.GetDatabase().Ping();
                        _logger.LogInformation($"Connected to Redis successfully. Ping result: {pingResult}ms");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to connect to Redis. Caching will be degraded to in-memory only.");
                        // Create a dummy connection multiplexer that will be used in case Redis is not available
                        lazyConnection = new Lazy<ConnectionMultiplexer>(() => 
                        {
                            try 
                            {
                                return ConnectionMultiplexer.Connect("localhost:0");
                            }
                            catch 
                            {
                                return null;
                            }
                        });
                    }
                }
            }
        }
        
        public ConnectionMultiplexer Connection 
        { 
            get 
            {
                try
                {
                    return lazyConnection.Value;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to get Redis connection");
                    return null;
                }
            } 
        }
    }
}
