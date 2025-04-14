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
            // Try to get from main cache
            var allLogs = await _cacheService.GetAsync<List<ActionLogDTO>>(RedisCacheKey.ACTION_LOG_ALL_CACHE_KEY);

            if (allLogs != null)
            {
                // Find the specific log in the cache
                var cachedLog = allLogs.FirstOrDefault(log => log.Id == id);
                if (cachedLog != null)
                {
                    _logger.LogInformation("ActionLog {id} returned from main cache", id);
                    return cachedLog;
                }
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

            // Update the main cache if it exists
            if (allLogs != null)
            {
                allLogs.Add(result);
                await _cacheService.SetAsync(RedisCacheKey.ACTION_LOG_ALL_CACHE_KEY, allLogs, _cacheExpirationTime);
            }
            else
            {
                // If main cache doesn't exist, load all logs and cache them
                allLogs = await LoadAllLogsFromDb();
                await _cacheService.SetAsync(RedisCacheKey.ACTION_LOG_ALL_CACHE_KEY, allLogs, _cacheExpirationTime);
            }

            return result;
        }

        public async Task<List<ActionLogDTO>> GetAllAsync(ActionLogQuery query)
        {
            // Get all logs from cache
            var allLogs = await _cacheService.GetAsync<List<ActionLogDTO>>(RedisCacheKey.ACTION_LOG_ALL_CACHE_KEY);

            if (allLogs == null)
            {
                // If not in cache, load all from database
                allLogs = await LoadAllLogsFromDb();
                await _cacheService.SetAsync(RedisCacheKey.ACTION_LOG_ALL_CACHE_KEY, allLogs, _cacheExpirationTime);
            }

            // Apply filters
            var filteredLogs = allLogs.AsQueryable();

            if (query.LogType.HasValue)
            {
                filteredLogs = filteredLogs.Where(x => x.LogType == query.LogType.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.SearchTerm))
            {
                var searchTerm = query.SearchTerm.ToLower();
                filteredLogs = filteredLogs.Where(x => 
                    (x.Title != null && x.Title.ToLower().Contains(searchTerm)) || 
                    (x.Description != null && x.Description.ToLower().Contains(searchTerm)));
            }

            if (query.FromDate.HasValue)
            {
                filteredLogs = filteredLogs.Where(x => x.CreatedAt >= query.FromDate.Value);
            }

            if (query.ToDate.HasValue)
            {
                filteredLogs = filteredLogs.Where(x => x.CreatedAt <= query.ToDate.Value);
            }

            // Convert to list for total count
            var filteredList = filteredLogs.ToList();
            query.Total = filteredList.Count;

            // Apply sorting and pagination
            return filteredList
                .OrderByDescending(x => x.CreatedAt)
                .Skip(query.Skip)
                .Take(query.PageSize)
                .ToList();
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

            // Invalidate the main cache
            await _cacheService.DeleteAsync(RedisCacheKey.ACTION_LOG_ALL_CACHE_KEY);

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

            // Invalidate the main cache
            await _cacheService.DeleteAsync(RedisCacheKey.ACTION_LOG_ALL_CACHE_KEY);

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

            // Invalidate the main cache
            await _cacheService.DeleteAsync(RedisCacheKey.ACTION_LOG_ALL_CACHE_KEY);

            return true;
        }

        public async Task<bool> InvalidateCacheAsync(int? id = null)
        {
            var tasks = new List<Task>();
            
            // Just invalidate the main cache
            tasks.Add(_cacheService.DeleteAsync(RedisCacheKey.ACTION_LOG_ALL_CACHE_KEY));
            
            await Task.WhenAll(tasks);
            return true;
        }

        // Helper method to load all logs from database
        private async Task<List<ActionLogDTO>> LoadAllLogsFromDb()
        {
            var logs = await _context.ActionLogs
                .AsNoTracking()
                .Where(x => !x.Deleted)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
            
            return logs.Select(MapToDTO).ToList();
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
    }
} 