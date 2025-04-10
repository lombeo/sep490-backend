using Microsoft.EntityFrameworkCore;

namespace Sep490_Backend.Infra.Entities
{
    public class LogWorkAmount : CommonEntity
    {
        public int Id { get; set; }
        public int LogId { get; set; }
        public int TaskIndex { get; set; }
        public decimal WorkAmount { get; set; }

        // Navigation properties
        public virtual ConstructionLog? ConstructionLog { get; set; }
    }

    public static class LogWorkAmountConfiguration
    {
        public static void Config(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LogWorkAmount>(entity =>
            {
                entity.ToTable("LogWorkAmounts");

                // Primary key
                entity.HasKey(e => e.Id);

                entity.Property(e => e.WorkAmount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp without time zone");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("timestamp without time zone");

                entity.Property(e => e.Deleted)
                    .HasDefaultValue(false);

                // Relationships
                entity.HasOne(e => e.ConstructionLog)
                    .WithMany(cl => cl.LogWorkAmounts)
                    .HasForeignKey(e => e.LogId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
} 