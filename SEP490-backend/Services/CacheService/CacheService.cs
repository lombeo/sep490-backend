using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace Sep490_Backend.Services.CacheService
{
    public interface ICacheService
    {
        Task DeleteAsync(string key, bool usingPrefix = true);

        Task DeleteAsync(List<string> keys, bool usingPrefix = true);
        
        /// <summary>
        /// Deletes all cache entries that start with the specified pattern
        /// </summary>
        /// <param name="pattern">The pattern to match keys for deletion</param>
        /// <param name="usingPrefix">Whether to use the cache prefix</param>
        Task DeleteByPatternAsync(string pattern, bool usingPrefix = true);

        Task<T> GetAsync<T>(string key, bool isMemoryCached = false);

        Task SetAsync<T>(string key, T value, TimeSpan? expirationTime = null, bool isMemoryCached = false);

        Task<string> GetAsync(string key, bool isMemoryCached = false);
    }

    public class CacheService : ICacheService
    {
        private readonly ILogger<CacheService> _logger;
        private readonly IDistributedCache _database;
        private readonly IMemoryCache _memoryCache;
        private readonly IConnectionMultiplexer _redisConnection;
        private const string _prefixCacheKey = "Sharing_";

        public CacheService(IDistributedCache cache, ILogger<CacheService> logger, IMemoryCache memoryCache, RedisConnManager redisConnManager)
        {
            _logger = logger;
            _database = cache;
            _memoryCache = memoryCache;
            _redisConnection = redisConnManager.Connection;
        }

        public async Task DeleteAsync(string key, bool usingPrefix = true)
        {
            try
            {
                if (usingPrefix)
                {
                    key = _prefixCacheKey + key;
                }
                _memoryCache.Remove(key);
                await _database.RemoveAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message + " " + ex.StackTrace);
            }
        }

        public async Task DeleteAsync(List<string> keys, bool usingPrefix = true)
        {
            try
            {
                foreach (var x in keys)
                {
                    await DeleteAsync(x, usingPrefix);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message + " " + ex.StackTrace);
            }
        }
        
        public async Task DeleteByPatternAsync(string pattern, bool usingPrefix = true)
        {
            try
            {
                if (usingPrefix)
                {
                    pattern = _prefixCacheKey + pattern;
                }
                
                // For memory cache, we can't easily find keys by pattern, so we'll need to rely on Redis
                
                try
                {
                    // Check if Redis connection is available
                    if (_redisConnection != null && _redisConnection.IsConnected)
                    {
                        // Get Redis database
                        var database = _redisConnection.GetDatabase();
                        
                        // Get Redis endpoints 
                        var endpoints = _redisConnection.GetEndPoints();
                        List<string> keysToDelete = new List<string>();
                        
                        // For each Redis server, get keys matching pattern
                        foreach (var endpoint in endpoints)
                        {
                            var server = _redisConnection.GetServer(endpoint);
                            // Add wildcard at end of pattern if not already present
                            string searchPattern = pattern.EndsWith("*") ? pattern : pattern + "*";
                            var keys = server.Keys(pattern: searchPattern).ToArray();
                            
                            foreach (var key in keys)
                            {
                                string keyString = key.ToString();
                                keysToDelete.Add(keyString);
                                
                                // Also remove from memory cache
                                if (usingPrefix && keyString.StartsWith(_prefixCacheKey))
                                {
                                    // Strip the prefix for memory cache key if using prefix mode
                                    string memoryCacheKey = keyString.Substring(_prefixCacheKey.Length);
                                    _memoryCache.Remove(memoryCacheKey);
                                }
                                else
                                {
                                    _memoryCache.Remove(keyString);
                                }
                            }
                        }
                        
                        // Delete all matching keys from Redis
                        if (keysToDelete.Count > 0)
                        {
                            foreach (var key in keysToDelete)
                            {
                                await database.KeyDeleteAsync(key);
                            }
                            _logger.LogInformation($"Deleted {keysToDelete.Count} keys matching pattern '{pattern}'");
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"Redis connection is not available. Using memory cache only for pattern '{pattern}'");
                        // Fall back to basic memory cache operations when Redis is not available
                        _memoryCache.Remove(pattern);
                    }
                }
                catch (RedisConnectionException redisEx)
                {
                    _logger.LogWarning($"Redis connection error while deleting by pattern '{pattern}': {redisEx.Message}");
                    _logger.LogWarning("Continuing with in-memory cache invalidation only");
                    
                    // If Redis fails, we'll at least clear the memory cache with the known pattern
                    // This is a best-effort approach since we can't easily find all memory cache keys by pattern
                    _memoryCache.Remove(pattern);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting cache by pattern '{pattern}': {ex.Message}");
                _logger.LogError(ex, ex.StackTrace);
            }
        }

        public async Task<T> GetAsync<T>(string key, bool isMemoryCached = false)
        {
            try
            {
                key = _prefixCacheKey + key;

                if (isMemoryCached)
                {
                    return _memoryCache.Get<T>(key) ?? default(T);
                }

                var result = await _database.GetStringAsync(key);
                if (!string.IsNullOrEmpty(result))
                {
                    // Configure JsonSerializerSettings to handle circular references
                    var jsonSettings = new JsonSerializerSettings
                    {
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                    };
                    
                    return JsonConvert.DeserializeObject<T>(result, jsonSettings);
                }

                return default(T);
            }
            catch (Exception ex)
            {
                _logger.LogError("GetAsync = " + key);
                _logger.LogError(ex, ex.Message + " " + ex.StackTrace);

                return default(T);
            }
        }

        public async Task<string> GetAsync(string key, bool isMemoryCached = false)
        {
            try
            {
                key = _prefixCacheKey + key;

                if (isMemoryCached)
                {
                    return _memoryCache.Get<string>(key);
                }

                return await _database.GetStringAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError("GetAsync = " + key);
                _logger.LogError(ex, ex.Message + " " + ex.StackTrace);

                return string.Empty;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expirationTime = null, bool isMemoryCached = false)
        {
            try
            {
                string keyWithPrefix = _prefixCacheKey + key;
                
                // Configure JsonSerializerSettings to handle circular references
                var jsonSettings = new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                };
                
                string serialized = JsonConvert.SerializeObject(value, jsonSettings);

                if (isMemoryCached)
                {
                    _memoryCache.Set(keyWithPrefix, value, expirationTime ?? TimeSpan.FromHours(1));
                }

                // Set for an hour by default or custom value
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expirationTime ?? TimeSpan.FromHours(1)
                };

                await _database.SetStringAsync(keyWithPrefix, serialized, options);
            }
            catch (Exception ex)
            {
                _logger.LogError("SetAsync = " + key);
                _logger.LogError(ex, ex.Message + " " + ex.StackTrace);
            }
        }
    }
}
