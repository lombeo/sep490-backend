using Microsoft.EntityFrameworkCore;
using Sep490_Backend.Infra.Enums;

namespace Sep490_Backend.Infra.Entities
{
    public class LogResource : CommonEntity
    {
        public int Id { get; set; }
        public int LogId { get; set; }
        public int TaskIndex { get; set; }
        public ResourceType ResourceType { get; set; }
        public int Quantity { get; set; }
        public string? ResourceId { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }

        // Navigation properties
        public virtual ConstructionLog? ConstructionLog { get; set; }
    }

    public static class LogResourceConfiguration
    {
        public static void Config(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LogResource>(entity =>
            {
                entity.ToTable("LogResources");

                // Primary key
                entity.HasKey(e => e.Id);

                entity.Property(e => e.StartTime)
                    .HasColumnType("timestamp without time zone");

                entity.Property(e => e.EndTime)
                    .HasColumnType("timestamp without time zone");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp without time zone");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("timestamp without time zone");

                entity.Property(e => e.Deleted)
                    .HasDefaultValue(false);

                // Relationships
                entity.HasOne(e => e.ConstructionLog)
                    .WithMany(cl => cl.LogResources)
                    .HasForeignKey(e => e.LogId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
} 