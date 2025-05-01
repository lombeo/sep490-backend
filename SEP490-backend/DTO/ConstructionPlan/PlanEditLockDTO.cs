using System;
using System.Text.Json.Serialization;

namespace Sep490_Backend.DTO.ConstructionPlan
{
    /// <summary>
    /// DTO for plan edit lock operations
    /// </summary>
    public class PlanEditLockDTO
    {
        public int Id { get; set; }
        public int PlanId { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string UserEmail { get; set; }
        public DateTime LockAcquiredAt { get; set; }
        public DateTime LockExpiresAt { get; set; }
        public bool IsCurrentUserLock { get; set; }
    }

    /// <summary>
    /// Request DTO for acquiring a lock
    /// </summary>
    public class AcquireLockDTO
    {
        [JsonRequired]
        public int PlanId { get; set; }
    }

    /// <summary>
    /// Request DTO for releasing a lock
    /// </summary>
    public class ReleaseLockDTO
    {
        [JsonRequired]
        public int PlanId { get; set; }
    }

    /// <summary>
    /// Response DTO for lock status
    /// </summary>
    public class LockStatusDTO
    {
        public bool Locked { get; set; }
        public PlanEditLockDTO LockInfo { get; set; }
    }

    /// <summary>
    /// DTO for extending a lock's expiration time
    /// </summary>
    public class ExtendLockDTO
    {
        [JsonRequired]
        public int PlanId { get; set; }
    }
} 