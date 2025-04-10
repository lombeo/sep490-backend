using Microsoft.EntityFrameworkCore;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.ConstructionLog;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Infra.Enums;
using Sep490_Backend.Controllers;
using System.Text.Json;

namespace Sep490_Backend.Services.ConstructionLogService
{
    public class ConstructionLogService : IConstructionLogService
    {
        private readonly BackendContext _context;

        public ConstructionLogService(BackendContext context)
        {
            _context = context;
        }

        public async Task<ConstructionLogDTO> GetByIdAsync(int id)
        {
            var constructionLog = await _context.ConstructionLogs
                .Include(cl => cl.LogResources)
                .Include(cl => cl.LogWorkAmounts)
                .FirstOrDefaultAsync(cl => cl.Id == id && !cl.Deleted);

            if (constructionLog == null)
                throw new ApiException(Message.ConstructionLogMessage.NOT_FOUND, $"Construction log with ID {id} not found");

            return MapEntityToDTO(constructionLog);
        }

        public async Task<List<ConstructionLogDTO>> GetAllAsync(ConstructionLogQueryDTO query)
        {
            var constructionLogsQuery = _context.ConstructionLogs
                .Include(cl => cl.LogResources)
                .Include(cl => cl.LogWorkAmounts)
                .Where(cl => !cl.Deleted);

            // Apply filters
            if (query.ProjectId.HasValue)
                constructionLogsQuery = constructionLogsQuery.Where(cl => cl.ProjectId == query.ProjectId.Value);

            if (query.FromDate.HasValue)
                constructionLogsQuery = constructionLogsQuery.Where(cl => cl.LogDate >= query.FromDate.Value);

            if (query.ToDate.HasValue)
                constructionLogsQuery = constructionLogsQuery.Where(cl => cl.LogDate <= query.ToDate.Value);

            if (!string.IsNullOrEmpty(query.SearchTerm))
                constructionLogsQuery = constructionLogsQuery.Where(cl => 
                    cl.LogCode.Contains(query.SearchTerm) || 
                    cl.LogName.Contains(query.SearchTerm));

            // Get total count for pagination
            query.Total = await constructionLogsQuery.CountAsync();

            // Apply pagination
            var constructionLogs = await constructionLogsQuery
                .OrderByDescending(cl => cl.LogDate)
                .Skip(query.Skip)
                .Take(query.PageSize)
                .ToListAsync();

            return constructionLogs.Select(MapEntityToDTO).ToList();
        }

        public async Task<ConstructionLogDTO> CreateAsync(ConstructionLogDTO dto, int userId)
        {
            // Check if user has permission to create
            if (!await CheckPermissionAsync(dto.ProjectId, userId, false))
                throw new ApiException(Message.ConstructionLogMessage.PERMISSION_DENIED, "You don't have permission to create construction logs for this project");

            // Create construction log
            var constructionLog = new ConstructionLog
            {
                LogCode = dto.LogCode,
                LogName = dto.LogName,
                LogDate = dto.LogDate,
                Safety = dto.Safety,
                Quality = dto.Quality,
                Progress = dto.Progress,
                Problem = dto.Problem,
                Advice = dto.Advice,
                Note = dto.Note,
                ProjectId = dto.ProjectId,
                Weather = SerializeWeather(dto.Weather),
                Images = dto.Images,
                Attachments = dto.Attachments,
                Creator = userId,
                Updater = userId
            };

            await _context.ConstructionLogs.AddAsync(constructionLog);
            await _context.SaveChangesAsync();

            // Add resources
            if (dto.Resources != null && dto.Resources.Any())
            {
                foreach (var resource in dto.Resources)
                {
                    var logResource = new LogResource
                    {
                        LogId = constructionLog.Id,
                        TaskIndex = resource.TaskIndex,
                        ResourceType = (ResourceType)resource.ResourceType,
                        Quantity = resource.Quantity,
                        ResourceId = resource.ResourceId,
                        StartTime = resource.StartTime,
                        EndTime = resource.EndTime,
                        Creator = userId,
                        Updater = userId
                    };

                    await _context.LogResources.AddAsync(logResource);
                }
            }

            // Add work amounts
            if (dto.WorkAmount != null && dto.WorkAmount.Any())
            {
                foreach (var workAmount in dto.WorkAmount)
                {
                    var logWorkAmount = new LogWorkAmount
                    {
                        LogId = constructionLog.Id,
                        TaskIndex = workAmount.TaskIndex,
                        WorkAmount = workAmount.WorkAmount,
                        Creator = userId,
                        Updater = userId
                    };

                    await _context.LogWorkAmounts.AddAsync(logWorkAmount);
                }
            }

            await _context.SaveChangesAsync();

            return await GetByIdAsync(constructionLog.Id);
        }

        public async Task<ConstructionLogDTO> UpdateAsync(ConstructionLogDTO dto, int userId)
        {
            var constructionLog = await _context.ConstructionLogs
                .Include(cl => cl.LogResources)
                .Include(cl => cl.LogWorkAmounts)
                .FirstOrDefaultAsync(cl => cl.Id == dto.Id && !cl.Deleted);

            if (constructionLog == null)
                throw new ApiException(Message.ConstructionLogMessage.NOT_FOUND, $"Construction log with ID {dto.Id} not found");

            // Check if user has permission to update
            if (!await CheckPermissionAsync(constructionLog.ProjectId, userId, false))
                throw new ApiException(Message.ConstructionLogMessage.PERMISSION_DENIED, "You don't have permission to update construction logs for this project");

            // Update construction log
            constructionLog.LogCode = dto.LogCode;
            constructionLog.LogName = dto.LogName;
            constructionLog.LogDate = dto.LogDate;
            constructionLog.Safety = dto.Safety;
            constructionLog.Quality = dto.Quality;
            constructionLog.Progress = dto.Progress;
            constructionLog.Problem = dto.Problem;
            constructionLog.Advice = dto.Advice;
            constructionLog.Note = dto.Note;
            constructionLog.Weather = SerializeWeather(dto.Weather);
            constructionLog.Images = dto.Images;
            constructionLog.Attachments = dto.Attachments;
            constructionLog.Updater = userId;

            // Update resources
            _context.LogResources.RemoveRange(constructionLog.LogResources);

            if (dto.Resources != null && dto.Resources.Any())
            {
                foreach (var resource in dto.Resources)
                {
                    var logResource = new LogResource
                    {
                        LogId = constructionLog.Id,
                        TaskIndex = resource.TaskIndex,
                        ResourceType = (ResourceType)resource.ResourceType,
                        Quantity = resource.Quantity,
                        ResourceId = resource.ResourceId,
                        StartTime = resource.StartTime,
                        EndTime = resource.EndTime,
                        Creator = userId,
                        Updater = userId
                    };

                    await _context.LogResources.AddAsync(logResource);
                }
            }

            // Update work amounts
            _context.LogWorkAmounts.RemoveRange(constructionLog.LogWorkAmounts);

            if (dto.WorkAmount != null && dto.WorkAmount.Any())
            {
                foreach (var workAmount in dto.WorkAmount)
                {
                    var logWorkAmount = new LogWorkAmount
                    {
                        LogId = constructionLog.Id,
                        TaskIndex = workAmount.TaskIndex,
                        WorkAmount = workAmount.WorkAmount,
                        Creator = userId,
                        Updater = userId
                    };

                    await _context.LogWorkAmounts.AddAsync(logWorkAmount);
                }
            }

            await _context.SaveChangesAsync();

            return await GetByIdAsync(constructionLog.Id);
        }

        public async Task<bool> DeleteAsync(int id, int userId)
        {
            var constructionLog = await _context.ConstructionLogs
                .FirstOrDefaultAsync(cl => cl.Id == id && !cl.Deleted);

            if (constructionLog == null)
                throw new ApiException(Message.ConstructionLogMessage.NOT_FOUND, $"Construction log with ID {id} not found");

            // Check if user has permission to delete
            if (!await CheckPermissionAsync(constructionLog.ProjectId, userId, false))
                throw new ApiException(Message.ConstructionLogMessage.PERMISSION_DENIED, "You don't have permission to delete construction logs for this project");

            constructionLog.Deleted = true;
            constructionLog.Updater = userId;

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> CheckPermissionAsync(int projectId, int userId, bool isViewRequest = true)
        {
            // Check if user exists
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.Deleted);
            if (user == null)
                return false;

            // Executive Board can view all projects
            if (user.Role == "ExecutiveBoard")
                return true;

            // Construction Manager can view and manage all projects
            if (user.Role == "ConstructionManager")
                return true;

            // For other roles, check if they have access to the project
            var projectUser = await _context.ProjectUsers
                .FirstOrDefaultAsync(pu => pu.ProjectId == projectId && pu.UserId == userId && !pu.Deleted);

            // If view request, any user related to the project can view
            if (isViewRequest)
                return projectUser != null;

            // For create/update/delete requests, only project creators can perform these actions
            return projectUser != null && projectUser.IsCreator;
        }

        private ConstructionLogDTO MapEntityToDTO(ConstructionLog entity)
        {
            return new ConstructionLogDTO
            {
                Id = entity.Id,
                LogCode = entity.LogCode,
                LogName = entity.LogName,
                LogDate = entity.LogDate,
                ProjectId = entity.ProjectId,
                Safety = entity.Safety,
                Quality = entity.Quality,
                Progress = entity.Progress,
                Problem = entity.Problem,
                Advice = entity.Advice,
                Note = entity.Note,
                Images = entity.Images,
                Attachments = entity.Attachments,
                Weather = DeserializeWeather(entity.Weather),
                Resources = entity.LogResources.Select(lr => new LogResourceDTO
                {
                    Id = lr.Id,
                    ResourceType = (int)lr.ResourceType,
                    TaskIndex = lr.TaskIndex,
                    Quantity = lr.Quantity,
                    ResourceId = lr.ResourceId ?? string.Empty,
                    StartTime = lr.StartTime,
                    EndTime = lr.EndTime
                }).ToList(),
                WorkAmount = entity.LogWorkAmounts.Select(lw => new LogWorkAmountDTO
                {
                    Id = lw.Id,
                    TaskIndex = lw.TaskIndex,
                    WorkAmount = lw.WorkAmount
                }).ToList()
            };
        }

        private JsonDocument? SerializeWeather(List<WeatherDTO>? weatherData)
        {
            if (weatherData == null || !weatherData.Any())
                return null;

            return JsonDocument.Parse(JsonSerializer.Serialize(weatherData));
        }

        private List<WeatherDTO> DeserializeWeather(JsonDocument? weatherData)
        {
            if (weatherData == null)
                return new List<WeatherDTO>();

            try
            {
                return JsonSerializer.Deserialize<List<WeatherDTO>>(weatherData.RootElement.GetRawText()) ?? new List<WeatherDTO>();
            }
            catch
            {
                return new List<WeatherDTO>();
            }
        }
    }
} 