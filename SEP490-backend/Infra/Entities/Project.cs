using Microsoft.EntityFrameworkCore;
using Sep490_Backend.Infra.Enums;
using System.Text.Json;

namespace Sep490_Backend.Infra.Entities
{
    public class Project : CommonEntity
    {
        public Project()
        {
            ProjectCode = string.Empty;
            ProjectName = string.Empty;
            SiteSurveys = new List<SiteSurvey>();
            ConstructionPlans = new List<ConstructionPlan>();
            ResourceMobilizationReqs = new List<ResourceMobilizationReqs>();
            ProjectUsers = new List<ProjectUser>();
        }

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

        // Navigation properties
        public virtual Customer? Customer { get; set; }
        public virtual Contract? Contract { get; set; }
        public virtual ICollection<SiteSurvey> SiteSurveys { get; set; }
        public virtual ICollection<ConstructionPlan> ConstructionPlans { get; set; }
        public virtual ICollection<ResourceMobilizationReqs> ResourceMobilizationReqs { get; set; }
        public virtual ICollection<ProjectUser> ProjectUsers { get; set; }
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

                // Relationships
                entity.HasOne(e => e.Customer)
                      .WithMany(c => c.Projects)
                      .HasForeignKey(e => e.CustomerId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Contract)
                      .WithOne(c => c.Project)
                      .HasForeignKey<Contract>(c => c.ProjectId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(e => e.SiteSurveys)
                      .WithOne(s => s.Project)
                      .HasForeignKey(s => s.ProjectId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.ProjectUsers)
                      .WithOne(pu => pu.Project)
                      .HasForeignKey(pu => pu.ProjectId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
