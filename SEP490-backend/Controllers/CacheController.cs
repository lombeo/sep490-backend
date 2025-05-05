using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Sep490_Backend.DTO;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Services.CacheService;

namespace Sep490_Backend.Controllers
{
    [ApiController]
    [Route("sep490/cache")]
    [Authorize]
    public class CacheController : BaseAPIController
    {
        private readonly ICacheService _cacheService;
        private readonly ILogger<CacheController> _logger;

        public CacheController(
            ICacheService cacheService,
            ILogger<CacheController> logger)
        {
            _cacheService = cacheService;
            _logger = logger;
        }

        /// <summary>
        /// Clears all caches, forcing data to be reloaded from the database
        /// </summary>
        /// <returns>Success message</returns>
        [HttpPost("clear-all")]
        public async Task<ResponseDTO<string>> ClearAllCaches()
        {
            try
            {
                // Clear all caches using pattern deletion
                await _cacheService.DeleteByPatternAsync("*");
                
                _logger.LogInformation($"User {UserId} manually cleared all caches");
                
                return new ResponseDTO<string>
                {
                    Code = (int)RESPONSE_CODE.OK,
                    Message = Message.CommonMessage.ACTION_SUCCESS,
                    Data = "All caches cleared successfully. Reload any pages to get fresh data."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all caches: {Message}", ex.Message);
                return new ResponseDTO<string>
                {
                    Code = (int)RESPONSE_CODE.InternalServerError,
                    Message = ex.Message,
                    Data = null
                };
            }
        }

        /// <summary>
        /// Clears all caches related to construction progress and logs
        /// </summary>
        /// <returns>Success message</returns>
        [HttpPost("clear-construction-data")]
        public async Task<ResponseDTO<string>> ClearConstructionDataCaches()
        {
            try
            {
                // Clear construction-related caches
                await _cacheService.DeleteByPatternAsync(RedisCacheKey.CONSTRUCTION_PROGRESS_ALL_PATTERN);
                await _cacheService.DeleteByPatternAsync(RedisCacheKey.CONSTRUCTION_LOG_ALL_PATTERN);
                await _cacheService.DeleteByPatternAsync(RedisCacheKey.INSPECTION_REPORT_ALL_PATTERN);
                await _cacheService.DeleteByPatternAsync("ConstructionProgressItem:*");
                await _cacheService.DeleteByPatternAsync("DASHBOARD:*");
                await _cacheService.DeleteByPatternAsync("STATISTICS:*");
                
                _logger.LogInformation($"User {UserId} cleared construction data caches");
                
                return new ResponseDTO<string>
                {
                    Code = (int)RESPONSE_CODE.OK,
                    Message = Message.CommonMessage.ACTION_SUCCESS,
                    Data = "Construction data caches cleared successfully. Reload any pages to get fresh data."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing construction data caches: {Message}", ex.Message);
                return new ResponseDTO<string>
                {
                    Code = (int)RESPONSE_CODE.InternalServerError,
                    Message = ex.Message,
                    Data = null
                };
            }
        }

        /// <summary>
        /// Clears caches related to a specific project
        /// </summary>
        /// <param name="projectId">ID of the project</param>
        /// <returns>Success message</returns>
        [HttpPost("clear-project/{projectId}")]
        public async Task<ResponseDTO<string>> ClearProjectCaches(int projectId)
        {
            try
            {
                // Clear project-specific caches
                await _cacheService.DeleteAsync(string.Format(RedisCacheKey.PROJECT_BY_ID_CACHE_KEY, projectId));
                await _cacheService.DeleteAsync(string.Format(RedisCacheKey.CONSTRUCTION_PROGRESS_BY_PROJECT_CACHE_KEY, projectId));
                await _cacheService.DeleteAsync(string.Format(RedisCacheKey.CONSTRUCTION_LOG_BY_PROJECT_CACHE_KEY, projectId));
                await _cacheService.DeleteAsync(string.Format(RedisCacheKey.INSPECTION_REPORT_BY_PROJECT_CACHE_KEY, projectId));
                await _cacheService.DeleteByPatternAsync($"ConstructionProgressItem:Project:{projectId}:*");
                
                // Clear general caches that might include this project's data
                await _cacheService.DeleteAsync(RedisCacheKey.PROJECT_CACHE_KEY);
                await _cacheService.DeleteAsync(RedisCacheKey.PROJECT_LIST_CACHE_KEY);
                await _cacheService.DeleteAsync(RedisCacheKey.PROJECT_STATUS_CACHE_KEY);
                await _cacheService.DeleteAsync(RedisCacheKey.CONSTRUCTION_PROGRESS_CACHE_KEY);
                await _cacheService.DeleteAsync(RedisCacheKey.CONSTRUCTION_LOG_CACHE_KEY);
                await _cacheService.DeleteAsync(RedisCacheKey.INSPECTION_REPORT_CACHE_KEY);
                
                _logger.LogInformation($"User {UserId} cleared caches for project {projectId}");
                
                return new ResponseDTO<string>
                {
                    Code = (int)RESPONSE_CODE.OK,
                    Message = Message.CommonMessage.ACTION_SUCCESS,
                    Data = $"All caches for project {projectId} cleared successfully. Reload any pages to get fresh data."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing project caches: {Message}", ex.Message);
                return new ResponseDTO<string>
                {
                    Code = (int)RESPONSE_CODE.InternalServerError,
                    Message = ex.Message,
                    Data = null
                };
            }
        }
    }
} 