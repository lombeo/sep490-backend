using Microsoft.EntityFrameworkCore;
using Sep490_Backend.DTO.ConstructionPlan;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Services.CacheService;
using System;
using System.Threading.Tasks;

namespace Sep490_Backend.Services.ConstructionPlanService
{
    /// <summary>
    /// Service for managing locks on construction plans during editing
    /// </summary>
    public interface IPlanEditLockService
    {
        /// <summary>
        /// Acquire a lock on a construction plan for editing
        /// </summary>
        Task<PlanEditLockDTO> AcquireLock(AcquireLockDTO model, int actionBy);

        /// <summary>
        /// Release a lock on a construction plan
        /// </summary>
        Task<bool> ReleaseLock(ReleaseLockDTO model, int actionBy);

        /// <summary>
        /// Check if a plan is currently locked and by whom
        /// </summary>
        Task<LockStatusDTO> GetLockStatus(int planId, int actionBy);

        /// <summary>
        /// Extend a lock's expiration time
        /// </summary>
        Task<PlanEditLockDTO> ExtendLock(ExtendLockDTO model, int actionBy);

        /// <summary>
        /// Clean up expired locks (to be run periodically)
        /// </summary>
        Task CleanupExpiredLocks();
    }

    public class PlanEditLockService : IPlanEditLockService
    {
        private readonly BackendContext _context;
        private readonly ICacheService _cacheService;
        
        // Lock duration in minutes
        private const int DEFAULT_LOCK_DURATION_MINUTES = 15;
        private const int LOCK_EXTENSION_MINUTES = 15;

        public PlanEditLockService(BackendContext context, ICacheService cacheService)
        {
            _context = context;
            _cacheService = cacheService;
        }

        /// <summary>
        /// Acquire a lock on a construction plan for editing
        /// </summary>
        public async Task<PlanEditLockDTO> AcquireLock(AcquireLockDTO model, int actionBy)
        {
            // First check if the construction plan exists
            var constructionPlan = await _context.ConstructionPlans
                .FirstOrDefaultAsync(cp => cp.Id == model.PlanId && !cp.Deleted);

            if (constructionPlan == null)
            {
                throw new KeyNotFoundException(Message.ConstructionPlanMessage.NOT_FOUND);
            }

            // Check if there is an existing active lock
            var existingLock = await _context.PlanEditLocks
                .Where(l => l.PlanId == model.PlanId && !l.Deleted && l.LockExpiresAt > DateTime.UtcNow)
                .FirstOrDefaultAsync();

            if (existingLock != null)
            {
                // If the lock belongs to the requesting user, extend it
                if (existingLock.UserId == actionBy)
                {
                    existingLock.LockExpiresAt = DateTime.UtcNow.AddMinutes(DEFAULT_LOCK_DURATION_MINUTES);
                    existingLock.Updater = actionBy;
                    existingLock.UpdatedAt = DateTime.UtcNow;

                    _context.PlanEditLocks.Update(existingLock);
                    await _context.SaveChangesAsync();

                    return await CreateLockDTO(existingLock, actionBy);
                }
                else
                {
                    // The plan is being edited by someone else
                    throw new InvalidOperationException(Message.ConstructionPlanMessage.PLAN_ALREADY_LOCKED);
                }
            }

            // Create a new lock
            var newLock = new PlanEditLock
            {
                PlanId = model.PlanId,
                UserId = actionBy,
                LockAcquiredAt = DateTime.UtcNow,
                LockExpiresAt = DateTime.UtcNow.AddMinutes(DEFAULT_LOCK_DURATION_MINUTES),
                Creator = actionBy,
                Updater = actionBy,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.PlanEditLocks.AddAsync(newLock);
            await _context.SaveChangesAsync();

            // Invalidate any cache entries related to this plan
            await InvalidatePlanLockCache(model.PlanId);

            return await CreateLockDTO(newLock, actionBy);
        }

        /// <summary>
        /// Release a lock on a construction plan
        /// </summary>
        public async Task<bool> ReleaseLock(ReleaseLockDTO model, int actionBy)
        {
            // Find active lock for this plan
            var existingLock = await _context.PlanEditLocks
                .Where(l => l.PlanId == model.PlanId && !l.Deleted && l.LockExpiresAt > DateTime.UtcNow)
                .FirstOrDefaultAsync();

            if (existingLock == null)
            {
                // No active lock found, nothing to do
                return true;
            }

            // Only the user who acquired the lock or an administrator can release it
            if (existingLock.UserId != actionBy)
            {
                throw new UnauthorizedAccessException(Message.ConstructionPlanMessage.LOCK_BELONGS_TO_ANOTHER_USER);
            }

            // Soft delete the lock
            existingLock.Deleted = true;
            existingLock.Updater = actionBy;
            existingLock.UpdatedAt = DateTime.UtcNow;

            _context.PlanEditLocks.Update(existingLock);
            await _context.SaveChangesAsync();

            // Invalidate any cache entries related to this plan
            await InvalidatePlanLockCache(model.PlanId);

            return true;
        }

        /// <summary>
        /// Check if a plan is currently locked and by whom
        /// </summary>
        public async Task<LockStatusDTO> GetLockStatus(int planId, int actionBy)
        {
            // First check if the construction plan exists
            var constructionPlan = await _context.ConstructionPlans
                .FirstOrDefaultAsync(cp => cp.Id == planId && !cp.Deleted);

            if (constructionPlan == null)
            {
                throw new KeyNotFoundException(Message.ConstructionPlanMessage.NOT_FOUND);
            }

            // Check if there is an existing active lock
            var existingLock = await _context.PlanEditLocks
                .Where(l => l.PlanId == planId && !l.Deleted && l.LockExpiresAt > DateTime.UtcNow)
                .FirstOrDefaultAsync();

            if (existingLock == null)
            {
                // No active lock found
                return new LockStatusDTO
                {
                    Locked = false,
                    LockInfo = null
                };
            }

            // Return lock information
            return new LockStatusDTO
            {
                Locked = true,
                LockInfo = await CreateLockDTO(existingLock, actionBy)
            };
        }

        /// <summary>
        /// Extend a lock's expiration time
        /// </summary>
        public async Task<PlanEditLockDTO> ExtendLock(ExtendLockDTO model, int actionBy)
        {
            // Find active lock for this plan
            var existingLock = await _context.PlanEditLocks
                .Where(l => l.PlanId == model.PlanId && !l.Deleted && l.LockExpiresAt > DateTime.UtcNow)
                .FirstOrDefaultAsync();

            if (existingLock == null)
            {
                throw new KeyNotFoundException(Message.ConstructionPlanMessage.LOCK_NOT_FOUND);
            }

            // Only the user who acquired the lock can extend it
            if (existingLock.UserId != actionBy)
            {
                throw new UnauthorizedAccessException(Message.ConstructionPlanMessage.LOCK_BELONGS_TO_ANOTHER_USER);
            }

            // Extend the lock
            existingLock.LockExpiresAt = DateTime.UtcNow.AddMinutes(LOCK_EXTENSION_MINUTES);
            existingLock.Updater = actionBy;
            existingLock.UpdatedAt = DateTime.UtcNow;

            _context.PlanEditLocks.Update(existingLock);
            await _context.SaveChangesAsync();

            // Invalidate any cache entries related to this plan
            await InvalidatePlanLockCache(model.PlanId);

            return await CreateLockDTO(existingLock, actionBy);
        }

        /// <summary>
        /// Clean up expired locks (to be run periodically)
        /// </summary>
        public async Task CleanupExpiredLocks()
        {
            var expiredLocks = await _context.PlanEditLocks
                .Where(l => !l.Deleted && l.LockExpiresAt <= DateTime.UtcNow)
                .ToListAsync();

            if (expiredLocks.Any())
            {
                foreach (var expiredLock in expiredLocks)
                {
                    expiredLock.Deleted = true;
                    expiredLock.UpdatedAt = DateTime.UtcNow;
                    // Use system user ID for cleanup operations
                    expiredLock.Updater = 1; // Assuming 1 is system user
                }

                _context.PlanEditLocks.UpdateRange(expiredLocks);
                await _context.SaveChangesAsync();

                // Invalidate cache for all affected plans
                foreach (var planId in expiredLocks.Select(l => l.PlanId).Distinct())
                {
                    await InvalidatePlanLockCache(planId);
                }
            }
        }

        /// <summary>
        /// Create a PlanEditLockDTO from a PlanEditLock entity
        /// </summary>
        private async Task<PlanEditLockDTO> CreateLockDTO(PlanEditLock lockEntity, int currentUserId)
        {
            // Get the user information for the lock owner
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == lockEntity.UserId);

            return new PlanEditLockDTO
            {
                Id = lockEntity.Id,
                PlanId = lockEntity.PlanId,
                UserId = lockEntity.UserId,
                UserName = user?.FullName ?? "Unknown User",
                UserEmail = user?.Email ?? "unknown@example.com",
                LockAcquiredAt = lockEntity.LockAcquiredAt,
                LockExpiresAt = lockEntity.LockExpiresAt,
                IsCurrentUserLock = lockEntity.UserId == currentUserId
            };
        }

        /// <summary>
        /// Invalidate cache entries related to a plan's lock status
        /// </summary>
        private async Task InvalidatePlanLockCache(int planId)
        {
            string planLockCacheKey = $"PLAN_LOCK:{planId}";
            await _cacheService.DeleteAsync(planLockCacheKey);
            await _cacheService.DeleteByPatternAsync("PLAN_LOCK:*");
        }
    }
} 