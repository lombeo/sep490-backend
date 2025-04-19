using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Sep490_Backend.Infra.Enums;

namespace Sep490_Backend.Infra.Entities
{
    public class ConstructPlanItemDetail : CommonEntity
    {
        public int Id { get; set; }
        public string PlanItemId { get; set; } //ConstructPlanItem - WorkCode
        public string WorkCode { get; set; }
        public ResourceType ResourceType { get; set; }
        public int Quantity { get; set; }
        public string? Unit { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total { get; set; }
        public int? ResourceId { get; set; } // Single resource ID

        // Navigation properties
        public virtual ConstructPlanItem ConstructPlanItem { get; set; }
    }

    public static class ConstructPlanItemDetailConfiguration
    {
        public static void Config(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ConstructPlanItemDetail>(entity =>
            {
                entity.ToTable("ConstructPlanItemDetails");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.PlanItemId)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.WorkCode)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.ResourceType)
                    .IsRequired();

                entity.Property(e => e.Quantity)
                    .IsRequired();

                entity.Property(e => e.Unit)
                    .HasMaxLength(50);

                entity.Property(e => e.UnitPrice)
                    .HasColumnType("numeric(18,2)");

                entity.Property(e => e.Total)
                    .HasColumnType("numeric(18,2)");
                
                entity.Property(e => e.ResourceId);

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp without time zone");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("timestamp without time zone");

                entity.HasIndex(e => e.PlanItemId);
                entity.HasIndex(e => e.WorkCode);
                entity.HasIndex(e => e.ResourceType);
                entity.HasIndex(e => e.ResourceId);

                // Relationships
                entity.HasOne(e => e.ConstructPlanItem)
                    .WithMany(cpi => cpi.ConstructPlanItemDetails)
                    .HasForeignKey(e => e.PlanItemId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
