using System.Text.Json;
using Sep490_Backend.Infra.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sep490_Backend.Infra.Entities
{
    public class InspectionReport : CommonEntity
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string InspectCode { get; set; }
        public int InspectorId { get; set; }
        public DateTime InspectStartDate { get; set; }
        public DateTime InspectEndDate { get; set; }
        public int ProgressId { get; set; }
        public int PlanId { get; set; }
        public string Location { get; set; }
        public JsonDocument Attachment { get; set; }
        public int InspectionDecision { get; set; }
        public int Status { get; set; }
        public string QualityNote { get; set; }
        public string OtherNote { get; set; }

        // Navigation properties
        public virtual Project Project { get; set; }
        public virtual User Inspector { get; set; }
        public virtual ConstructionProgress Progress { get; set; }
        public virtual ConstructionPlan Plan { get; set; }
    }

    public static class InspectionReportConfiguration
    {
        public static void Config(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
        {
            var entity = modelBuilder.Entity<InspectionReport>();

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.Property(e => e.InspectCode).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Location).HasMaxLength(255);
            entity.Property(e => e.QualityNote).HasMaxLength(2000);
            entity.Property(e => e.OtherNote).HasMaxLength(2000);

            // Relations
            entity.HasOne(e => e.Project)
                .WithMany()
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);

            entity.HasOne(e => e.Inspector)
                .WithMany()
                .HasForeignKey(e => e.InspectorId)
                .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);

            entity.HasOne(e => e.Progress)
                .WithMany()
                .HasForeignKey(e => e.ProgressId)
                .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);

            entity.HasOne(e => e.Plan)
                .WithMany()
                .HasForeignKey(e => e.PlanId)
                .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
        }
    }
} 