using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.ConstructionLog;
using Sep490_Backend.Infra.Entities;

namespace Sep490_Backend.Services.ConstructionLogService
{
    public interface IConstructionLogService
    {
        Task<ConstructionLogDTO> GetByIdAsync(int id);
        Task<List<ConstructionLogDTO>> GetAllAsync(ConstructionLogQueryDTO query);
        Task<ConstructionLogDTO> CreateAsync(ConstructionLogDTO dto, int userId);
        Task<ConstructionLogDTO> UpdateAsync(ConstructionLogDTO dto, int userId);
        Task<bool> DeleteAsync(int id, int userId);
        Task<bool> CheckPermissionAsync(int projectId, int userId, bool isViewRequest = true);
    }
} 