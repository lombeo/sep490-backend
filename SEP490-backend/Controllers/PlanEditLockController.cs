using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.ConstructionPlan;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Services.ConstructionPlanService;
using System.Threading.Tasks;

namespace Sep490_Backend.Controllers
{
    [Route(RouteApiConstant.BASE_PATH + "/plans/locks")]
    [ApiController]
    [Authorize]
    public class PlanEditLockController : BaseAPIController
    {
        private readonly IPlanEditLockService _planEditLockService;

        public PlanEditLockController(IPlanEditLockService planEditLockService)
        {
            _planEditLockService = planEditLockService;
        }

        /// <summary>
        /// Acquire a lock on a construction plan for editing
        /// </summary>
        [HttpPost("acquire")]
        public async Task<ResponseDTO<PlanEditLockDTO>> AcquireLock([FromBody] AcquireLockDTO model)
        {
            return await HandleException(_planEditLockService.AcquireLock(model, UserId), Message.ConstructionPlanMessage.LOCK_ACQUIRED_SUCCESS);
        }

        /// <summary>
        /// Release a lock on a construction plan
        /// </summary>
        [HttpPost("release")]
        public async Task<ResponseDTO<bool>> ReleaseLock([FromBody] ReleaseLockDTO model)
        {
            return await HandleException(_planEditLockService.ReleaseLock(model, UserId), Message.ConstructionPlanMessage.LOCK_RELEASED_SUCCESS);
        }

        /// <summary>
        /// Check if a plan is currently locked and by whom
        /// </summary>
        [HttpGet("status/{planId}")]
        public async Task<ResponseDTO<LockStatusDTO>> GetLockStatus(int planId)
        {
            return await HandleException(_planEditLockService.GetLockStatus(planId, UserId), Message.ConstructionPlanMessage.LOCK_STATUS_SUCCESS);
        }

        /// <summary>
        /// Extend a lock's expiration time
        /// </summary>
        [HttpPost("extend")]
        public async Task<ResponseDTO<PlanEditLockDTO>> ExtendLock([FromBody] ExtendLockDTO model)
        {
            return await HandleException(_planEditLockService.ExtendLock(model, UserId), Message.ConstructionPlanMessage.LOCK_EXTENDED_SUCCESS);
        }
    }
} 