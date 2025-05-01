using Microsoft.EntityFrameworkCore;
using Sep490_Backend.Infra.Entities;
using System;

namespace Sep490_Backend.Infra.Entities
{
    /// <summary>
    /// Entity class for tracking which users are currently editing construction plans
    /// </summary>
    public class PlanEditLock : CommonEntity
    {
        public int Id { get; set; }
        public int PlanId { get; set; }
        public int UserId { get; set; }
        public DateTime LockAcquiredAt { get; set; }
        public DateTime LockExpiresAt { get; set; }
        
        // Navigation property
        public virtual ConstructionPlan ConstructionPlan { get; set; }
        public virtual User User { get; set; }
    }

    public static class PlanEditLockConfiguration
    {
        public static void Config(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PlanEditLock>(entity =>
            {
                entity.ToTable("PlanEditLocks");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.PlanId)
                    .IsRequired();

                entity.Property(e => e.UserId)
                    .IsRequired();

                entity.Property(e => e.LockAcquiredAt)
                    .HasColumnType("timestamp without time zone")
                    .IsRequired();

                entity.Property(e => e.LockExpiresAt)
                    .HasColumnType("timestamp without time zone")
                    .IsRequired();

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp without time zone");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("timestamp without time zone");

                // Create indexes
                entity.HasIndex(e => e.PlanId);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.LockExpiresAt);

                // Relationships
                entity.HasOne(e => e.ConstructionPlan)
                    .WithMany()
                    .HasForeignKey(e => e.PlanId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
} 