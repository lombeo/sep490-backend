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
        public virtual Vehicle Vehicle { get; set; }
        public virtual User User { get; set; }
        public virtual Material Material { get; set; }
        public virtual ConstructionTeam ConstructionTeam { get; set; } // For HUMAN resource type
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

                // One-to-one relationship with resources based on ResourceType
                entity.HasOne(e => e.Vehicle)
                    .WithMany()
                    .HasForeignKey(e => e.ResourceId)
                    .HasConstraintName("FK_ConstructPlanItemDetails_Vehicles_ResourceId")
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.ResourceId)
                    .HasConstraintName("FK_ConstructPlanItemDetails_Users_ResourceId")
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.Material)
                    .WithMany()
                    .HasForeignKey(e => e.ResourceId)
                    .HasConstraintName("FK_ConstructPlanItemDetails_Materials_ResourceId")
                    .OnDelete(DeleteBehavior.SetNull);
                    
                entity.HasOne(e => e.ConstructionTeam)
                    .WithMany()
                    .HasForeignKey(e => e.ResourceId)
                    .HasConstraintName("FK_ConstructPlanItemDetails_ConstructionTeams_ResourceId")
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Add query filters based on resource type
            modelBuilder.Entity<ConstructPlanItemDetail>()
                .HasQueryFilter(e => e.ResourceType == ResourceType.MACHINE && e.Vehicle != null);

            modelBuilder.Entity<ConstructPlanItemDetail>()
                .HasQueryFilter(e => (e.ResourceType == ResourceType.HUMAN && e.ConstructionTeam != null) || e.User != null);

            modelBuilder.Entity<ConstructPlanItemDetail>()
                .HasQueryFilter(e => e.ResourceType == ResourceType.MATERIAL && e.Material != null);
        }
    }
}
