using Sep490_Backend.DTO.ActionLog;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.Infra.Entities;

namespace Sep490_Backend.Services.ActionLogService
{
    public interface IActionLogService
    {
        Task<ActionLogDTO> GetByIdAsync(int id);
        Task<List<ActionLogDTO>> GetAllAsync(ActionLogQuery query);
        Task<ActionLogDTO> CreateAsync(ActionLogCreateDTO dto, int userId);
        Task<ActionLogDTO> UpdateAsync(int id, ActionLogUpdateDTO dto, int userId);
        Task<bool> DeleteAsync(int id);
        Task<bool> InvalidateCacheAsync(int? id = null);
    }
} 