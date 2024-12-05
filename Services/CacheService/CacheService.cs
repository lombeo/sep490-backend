using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;

namespace Api_Project_Prn.Services.CacheService
{
    public interface ICacheService
    {
        Task DeleteAsync(string key, bool usingPrefix = true);

        Task DeleteAsync(List<string> keys, bool usingPrefix = true);

        Task<T> GetAsync<T>(string key, bool isMemoryCached = false);

        Task SetAsync<T>(string key, T value, TimeSpan? expirationTime = null, bool isMemoryCached = false);

        Task<string> GetAsync(string key, bool isMemoryCached = false);
    }

    public class CacheService : ICacheService
    {
        private readonly ILogger<CacheService> _logger;
        private readonly IDistributedCache _database;
        private readonly IMemoryCache _memoryCache;
        private const string _prefixCacheKey = "Sharing_";

        public CacheService(IDistributedCache cache, ILogger<CacheService> logger, IMemoryCache memoryCache)
        {
            _logger = logger;
            _database = cache;
            _memoryCache = memoryCache;
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
                    return JsonConvert.DeserializeObject<T>(result);
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
                key = _prefixCacheKey + key;

                expirationTime = expirationTime ?? new TimeSpan(24, 0, 0);
                if (isMemoryCached)
                {
                    _memoryCache.Set(key, value, expirationTime.Value);
                }
                else
                {
                    await _database.SetStringAsync(key, JsonConvert.SerializeObject(value), new DistributedCacheEntryOptions() { SlidingExpiration = expirationTime });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("SetAsync key= " + key);
                _logger.LogError("SetAsync value = " + JsonConvert.SerializeObject(value));
                _logger.LogError(ex, ex.Message + " " + ex.StackTrace);
            }
        }
    }
}
