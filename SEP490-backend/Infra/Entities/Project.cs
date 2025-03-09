using Microsoft.EntityFrameworkCore;
using Sep490_Backend.Infra.Enums;
using System.Text.Json;

namespace Sep490_Backend.Infra.Entities
{
    public class Project : CommonEntity
    {
        public int Id { get; set; }
        public string ProjectCode { get; set; }
        public string ProjectName { get; set; }
        public int CustomerId { get; set; }
        public string? ConstructType { get; set; }
        public string? Location { get; set; }
        public string? Area { get; set; }
        public string? Purpose { get; set; }
        public string? TechnicalReqs { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal Budget { get; set; }
        public ProjectStatusEnum Status { get; set; }
        public JsonDocument? Attachments { get; set; }
        public string? Description { get; set; }
    }

    public static class ProjectConfiguration
    {
        public static void Config(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Project>(entity =>
            {
                entity.ToTable("Projects");

                // Khóa chính
                entity.HasKey(e => e.Id);

                entity.Property(e => e.ProjectCode)
                    .IsRequired();

                entity.Property(e => e.ProjectName)
                    .IsRequired();

                entity.Property(e => e.StartDate)
                    .IsRequired()
                    .HasColumnType("timestamp without time zone");

                entity.Property(e => e.EndDate)
                    .IsRequired()
                    .HasColumnType("timestamp without time zone");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp without time zone");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("timestamp without time zone");

                entity.Property(e => e.Budget)
                    .HasColumnType("numeric(18,2)");

                entity.Property(e => e.Location)
                    .HasColumnType("text");
                entity.Property(e => e.Area)
                    .HasColumnType("text");
                entity.Property(e => e.Purpose)
                    .HasColumnType("text");
                entity.Property(e => e.TechnicalReqs)
                    .HasColumnType("text");
                entity.Property(e => e.Attachments)
                    .HasColumnType("jsonb");
                entity.Property(e => e.Description)
                    .HasColumnType("text");

                entity.Property(e => e.Deleted)
                    .HasDefaultValue(false);
            });
        }
    }
}
