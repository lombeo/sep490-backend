using System.Text.Json;
using Sep490_Backend.Infra.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sep490_Backend.Infra.Entities
{
    public class InspectionReport : CommonEntity
    {
        public int Id { get; set; }
        public string InspectCode { get; set; }
        public int InspectorId { get; set; }
        public DateTime InspectStartDate { get; set; }
        public DateTime InspectEndDate { get; set; }
        public int ConstructionProgressItemId { get; set; }
        public string Location { get; set; }
        public JsonDocument Attachment { get; set; }
        public int InspectionDecision { get; set; }
        public int Status { get; set; }
        public string QualityNote { get; set; }
        public string OtherNote { get; set; }

        // Navigation properties
        public virtual User Inspector { get; set; }
        public virtual ConstructionProgressItem ConstructionProgressItem { get; set; }
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
            entity.HasOne(e => e.Inspector)
                .WithMany()
                .HasForeignKey(e => e.InspectorId)
                .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);

            entity.HasOne(e => e.ConstructionProgressItem)
                .WithMany()
                .HasForeignKey(e => e.ConstructionProgressItemId)
                .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
        }
    }
} 