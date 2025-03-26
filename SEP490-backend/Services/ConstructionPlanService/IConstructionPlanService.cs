using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.ConstructionPlan;
using Sep490_Backend.Infra.Entities;

namespace Sep490_Backend.Services.ConstructionPlanService
{
    public interface IConstructionPlanService
    {
        Task<List<ConstructionPlanDTO>> Search(ConstructionPlanQuery query);
        Task<ConstructionPlanDTO> Create(SaveConstructionPlanDTO model, int actionBy);
        Task<ConstructionPlanDTO> Update(SaveConstructionPlanDTO model, int actionBy);
        Task<ConstructionPlanDTO> GetById(int id, int actionBy);
        Task<bool> Delete(int id, int actionBy);
        Task<bool> Approve(ApproveConstructionPlanDTO model, int actionBy);
        Task<bool> Reject(ApproveConstructionPlanDTO model, int actionBy);
        Task<ConstructionPlanDTO> Import(ImportConstructionPlanDTO model, int actionBy);
        Task<bool> AssignTeam(AssignTeamDTO model, int actionBy);
    }
} 