using Microsoft.EntityFrameworkCore;
using Sep490_Backend.Infra.Enums;
using System.Text.Json;

namespace Sep490_Backend.Infra.Entities
{
    public class ConstructionLog : CommonEntity
    {
        public ConstructionLog()
        {
            LogCode = string.Empty;
            LogName = string.Empty;
            LogResources = new List<LogResource>();
            LogWorkAmounts = new List<LogWorkAmount>();
        }

        public int Id { get; set; }
        public string LogCode { get; set; }
        public string LogName { get; set; }
        public DateTime LogDate { get; set; }
        public int? Safety { get; set; }
        public int? Quality { get; set; }
        public int? Progress { get; set; }
        public string? Problem { get; set; }
        public string? Advice { get; set; }
        public string? Note { get; set; }
        public JsonDocument? Images { get; set; }
        public JsonDocument? Attachments { get; set; }
        public JsonDocument? Weather { get; set; }
        public int ProjectId { get; set; }

        // Navigation properties
        public virtual Project? Project { get; set; }
        public virtual ICollection<LogResource> LogResources { get; set; }
        public virtual ICollection<LogWorkAmount> LogWorkAmounts { get; set; }
    }

    public static class ConstructionLogConfiguration
    {
        public static void Config(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ConstructionLog>(entity =>
            {
                entity.ToTable("ConstructionLogs");

                // Primary key
                entity.HasKey(e => e.Id);

                entity.Property(e => e.LogCode)
                    .IsRequired();

                entity.Property(e => e.LogName)
                    .IsRequired();

                entity.Property(e => e.LogDate)
                    .IsRequired()
                    .HasColumnType("timestamp without time zone");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp without time zone");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("timestamp without time zone");

                entity.Property(e => e.Problem)
                    .HasColumnType("text");

                entity.Property(e => e.Advice)
                    .HasColumnType("text");

                entity.Property(e => e.Note)
                    .HasColumnType("text");

                entity.Property(e => e.Images)
                    .HasColumnType("jsonb");

                entity.Property(e => e.Attachments)
                    .HasColumnType("jsonb");

                entity.Property(e => e.Weather)
                    .HasColumnType("jsonb");

                entity.Property(e => e.Deleted)
                    .HasDefaultValue(false);

                // Relationships
                entity.HasOne(e => e.Project)
                    .WithMany()
                    .HasForeignKey(e => e.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
} 