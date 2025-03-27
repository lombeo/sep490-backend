using Microsoft.EntityFrameworkCore;
using Sep490_Backend.DTO.ActionLog;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Services.CacheService;
using System.Text;

namespace Sep490_Backend.Services.ActionLogService
{
    public class ActionLogService : IActionLogService
    {
        private readonly BackendContext _context;
        private readonly ICacheService _cacheService;
        private readonly ILogger<ActionLogService> _logger;
        private readonly TimeSpan _cacheExpirationTime = TimeSpan.FromMinutes(30);

        public ActionLogService(
            BackendContext context,
            ICacheService cacheService,
            ILogger<ActionLogService> logger)
        {
            _context = context;
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task<ActionLogDTO> GetByIdAsync(int id)
        {
            // Try to get from cache first
            var cacheKey = string.Format(RedisCacheKey.ACTION_LOG_BY_ID_CACHE_KEY, id);
            var cachedLog = await _cacheService.GetAsync<ActionLogDTO>(cacheKey, true);

            if (cachedLog != null)
            {
                _logger.LogInformation("ActionLog {id} returned from cache", id);
                return cachedLog;
            }

            // If not in cache, get from database
            var actionLog = await _context.ActionLogs
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && !x.Deleted);

            if (actionLog == null)
            {
                throw new KeyNotFoundException(Message.ActionLogMessage.NOT_FOUND);
            }

            var result = MapToDTO(actionLog);

            // Store in cache
            await _cacheService.SetAsync(cacheKey, result, _cacheExpirationTime, true);

            return result;
        }

        public async Task<List<ActionLogDTO>> GetAllAsync(ActionLogQuery query)
        {
            // For search and filtering, we don't use cache directly
            // Build cache key based on query parameters
            var cacheKey = BuildCacheKey(query);
            var cachedResult = await _cacheService.GetAsync<List<ActionLogDTO>>(cacheKey, true);

            if (cachedResult != null)
            {
                _logger.LogInformation("ActionLogs returned from cache with key {cacheKey}", cacheKey);
                return cachedResult;
            }

            // Build the query
            var actionLogsQuery = _context.ActionLogs
                .AsNoTracking()
                .Where(x => !x.Deleted);

            // Apply filters
            if (query.LogType.HasValue)
            {
                actionLogsQuery = actionLogsQuery.Where(x => x.LogType == query.LogType.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.SearchTerm))
            {
                var searchTerm = query.SearchTerm.ToLower();
                actionLogsQuery = actionLogsQuery.Where(x => 
                    (x.Title != null && x.Title.ToLower().Contains(searchTerm)) || 
                    (x.Description != null && x.Description.ToLower().Contains(searchTerm)));
            }

            if (query.FromDate.HasValue)
            {
                actionLogsQuery = actionLogsQuery.Where(x => x.CreatedAt >= query.FromDate.Value);
            }

            if (query.ToDate.HasValue)
            {
                actionLogsQuery = actionLogsQuery.Where(x => x.CreatedAt <= query.ToDate.Value);
            }

            // Get total count for pagination
            query.Total = await actionLogsQuery.CountAsync();

            // Apply pagination
            var paginatedItems = await actionLogsQuery
                .OrderByDescending(x => x.CreatedAt)
                .Skip(query.Skip)
                .Take(query.PageSize)
                .ToListAsync();

            // Map to DTOs
            var items = paginatedItems.Select(MapToDTO).ToList();

            // Cache the result
            await _cacheService.SetAsync(cacheKey, items, _cacheExpirationTime, true);

            return items;
        }

        public async Task<ActionLogDTO> CreateAsync(ActionLogCreateDTO dto, int userId)
        {
            var actionLog = new ActionLog
            {
                LogType = dto.LogType,
                Title = dto.Title,
                Description = dto.Description,
                Creator = userId,
                Updater = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.ActionLogs.Add(actionLog);
            await _context.SaveChangesAsync();

            var result = MapToDTO(actionLog);

            // Cache the new entity
            var cacheKey = string.Format(RedisCacheKey.ACTION_LOG_BY_ID_CACHE_KEY, result.Id);
            await _cacheService.SetAsync(cacheKey, result, _cacheExpirationTime, true);

            // Invalidate the collection cache
            await InvalidateCacheAsync();

            return result;
        }

        public async Task<ActionLogDTO> UpdateAsync(int id, ActionLogUpdateDTO dto, int userId)
        {
            var actionLog = await _context.ActionLogs
                .FirstOrDefaultAsync(x => x.Id == id && !x.Deleted);

            if (actionLog == null)
            {
                throw new KeyNotFoundException(Message.ActionLogMessage.NOT_FOUND);
            }

            actionLog.LogType = dto.LogType;
            actionLog.Title = dto.Title;
            actionLog.Description = dto.Description;
            actionLog.Updater = userId;
            actionLog.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var result = MapToDTO(actionLog);

            // Update cache
            var cacheKey = string.Format(RedisCacheKey.ACTION_LOG_BY_ID_CACHE_KEY, id);
            await _cacheService.SetAsync(cacheKey, result, _cacheExpirationTime, true);

            // Invalidate collection cache
            await InvalidateCacheAsync();

            return result;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var actionLog = await _context.ActionLogs
                .FirstOrDefaultAsync(x => x.Id == id && !x.Deleted);

            if (actionLog == null)
            {
                throw new KeyNotFoundException(Message.ActionLogMessage.NOT_FOUND);
            }

            actionLog.Deleted = true;
            actionLog.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Invalidate caches
            await InvalidateCacheAsync(id);

            return true;
        }

        public async Task<bool> InvalidateCacheAsync(int? id = null)
        {
            try
            {
                var tasks = new List<Task>();

                // If specific id is provided, invalidate that cache entry
                if (id.HasValue)
                {
                    tasks.Add(_cacheService.DeleteAsync(string.Format(RedisCacheKey.ACTION_LOG_BY_ID_CACHE_KEY, id.Value)));
                }

                // Always invalidate the collection cache with any prefix
                tasks.Add(_cacheService.DeleteAsync(RedisCacheKey.ACTION_LOG_ALL_CACHE_KEY));

                // Get all cache keys matching the pattern for collection caches with query parameters
                // In a real-world scenario, you might need a more sophisticated way to invalidate
                // all query-specific cache keys, possibly by storing them

                await Task.WhenAll(tasks);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating ActionLog cache");
                return false;
            }
        }

        private ActionLogDTO MapToDTO(ActionLog entity)
        {
            return new ActionLogDTO
            {
                Id = entity.Id,
                LogType = entity.LogType,
                Title = entity.Title,
                Description = entity.Description,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                Creator = entity.Creator,
                Updater = entity.Updater
            };
        }

        private string BuildCacheKey(ActionLogQuery query)
        {
            var keyBuilder = new StringBuilder(RedisCacheKey.ACTION_LOG_ALL_CACHE_KEY);

            keyBuilder.Append($"_Page{query.PageIndex}_Size{query.PageSize}");

            if (query.LogType.HasValue)
            {
                keyBuilder.Append($"_Type{query.LogType.Value}");
            }

            if (!string.IsNullOrWhiteSpace(query.SearchTerm))
            {
                keyBuilder.Append($"_Search{query.SearchTerm}");
            }

            if (query.FromDate.HasValue)
            {
                keyBuilder.Append($"_From{query.FromDate.Value:yyyyMMdd}");
            }

            if (query.ToDate.HasValue)
            {
                keyBuilder.Append($"_To{query.ToDate.Value:yyyyMMdd}");
            }

            return keyBuilder.ToString();
        }
    }
} 