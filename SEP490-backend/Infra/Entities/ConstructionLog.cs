using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Sep490_Backend.Infra.Entities
{
    public enum ConstructionLogStatus
    {
        Rejected = 0,
        Approved = 1,
        WaitingForApproval = 2
    }

    public class ConstructionLog : CommonEntity
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string LogCode { get; set; }
        public string LogName { get; set; }
        public DateTime LogDate { get; set; }
        public JsonDocument Resources { get; set; }
        public JsonDocument WorkAmount { get; set; }
        public JsonDocument Weather { get; set; }
        public string Safety { get; set; }
        public string Quality { get; set; }
        public string Progress { get; set; }
        public string Problem { get; set; }
        public string Advice { get; set; }
        public JsonDocument Images { get; set; }
        public JsonDocument Attachments { get; set; }
        public string Note { get; set; }
        public ConstructionLogStatus Status { get; set; } = ConstructionLogStatus.WaitingForApproval;

        // Navigation property
        public virtual Project Project { get; set; }
    }

    public static class ConstructionLogConfiguration
    {
        public static void Config(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ConstructionLog>(entity =>
            {
                entity.ToTable("ConstructionLogs");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.ProjectId)
                      .IsRequired();

                entity.Property(e => e.LogCode)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.Property(e => e.LogName)
                      .IsRequired()
                      .HasMaxLength(255);

                entity.Property(e => e.LogDate)
                      .IsRequired()
                      .HasColumnType("timestamp without time zone");

                entity.Property(e => e.Resources)
                      .HasColumnType("jsonb");

                entity.Property(e => e.WorkAmount)
                      .HasColumnType("jsonb");

                entity.Property(e => e.Weather)
                      .HasColumnType("jsonb");

                entity.Property(e => e.Safety)
                      .HasMaxLength(2000);

                entity.Property(e => e.Quality)
                      .HasMaxLength(2000);

                entity.Property(e => e.Progress)
                      .HasMaxLength(2000);

                entity.Property(e => e.Problem)
                      .HasMaxLength(2000);

                entity.Property(e => e.Advice)
                      .HasMaxLength(2000);

                entity.Property(e => e.Images)
                      .HasColumnType("jsonb");

                entity.Property(e => e.Attachments)
                      .HasColumnType("jsonb");

                entity.Property(e => e.Note)
                      .HasMaxLength(2000);

                entity.Property(e => e.Status)
                      .HasDefaultValue(ConstructionLogStatus.WaitingForApproval);

                entity.Property(e => e.CreatedAt)
                      .HasColumnType("timestamp without time zone");

                entity.Property(e => e.UpdatedAt)
                      .HasColumnType("timestamp without time zone");

                entity.Property(e => e.Deleted)
                      .HasDefaultValue(false);

                // Relationships
                entity.HasOne(e => e.Project)
                      .WithMany()
                      .HasForeignKey(e => e.ProjectId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Indexes
                entity.HasIndex(e => e.ProjectId);
                entity.HasIndex(e => e.LogCode).IsUnique();
                entity.HasIndex(e => e.LogDate);
            });
        }
    }
} 