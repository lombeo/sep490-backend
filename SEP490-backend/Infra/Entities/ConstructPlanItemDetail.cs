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

        // Navigation properties
        public virtual ConstructPlanItem ConstructPlanItem { get; set; }
        public virtual ICollection<Vehicle> Vehicles { get; set; }
        public virtual ICollection<User> Users { get; set; }
        public virtual ICollection<Material> Materials { get; set; }
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

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp without time zone");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("timestamp without time zone");

                entity.HasIndex(e => e.PlanItemId);
                entity.HasIndex(e => e.WorkCode);
                entity.HasIndex(e => e.ResourceType);

                // Relationships
                entity.HasOne(e => e.ConstructPlanItem)
                    .WithMany(cpi => cpi.ConstructPlanItemDetails)
                    .HasForeignKey(e => e.PlanItemId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Many-to-many with Vehicle
                entity.HasMany(e => e.Vehicles)
                    .WithMany(v => v.ConstructPlanItemDetails)
                    .UsingEntity<Dictionary<string, object>>(
                        "ConstructPlanItemDetailVehicles",
                        j => j
                            .HasOne<Vehicle>()
                            .WithMany()
                            .HasForeignKey("VehicleId")
                            .HasConstraintName("FK_ConstructPlanItemDetailVehicles_Vehicles_VehicleId")
                            .OnDelete(DeleteBehavior.Cascade),
                        j => j
                            .HasOne<ConstructPlanItemDetail>()
                            .WithMany()
                            .HasForeignKey("ConstructPlanItemDetailId")
                            .HasConstraintName("FK_ConstructPlanItemDetailVehicles_ConstructPlanItemDetails_ConstructPlanItemDetailId")
                            .OnDelete(DeleteBehavior.Cascade),
                        j => 
                        {
                            j.HasKey("VehicleId", "ConstructPlanItemDetailId");
                            j.ToTable("ConstructPlanItemDetailVehicles");
                        });

                // Many-to-many with User
                entity.HasMany(e => e.Users)
                    .WithMany(u => u.ResourceAllocations)
                    .UsingEntity<Dictionary<string, object>>(
                        "ConstructPlanItemDetailUsers",
                        j => j
                            .HasOne<User>()
                            .WithMany()
                            .HasForeignKey("UserId")
                            .HasConstraintName("FK_ConstructPlanItemDetailUsers_Users_UserId")
                            .OnDelete(DeleteBehavior.Cascade),
                        j => j
                            .HasOne<ConstructPlanItemDetail>()
                            .WithMany()
                            .HasForeignKey("ResourceAllocationId")
                            .HasConstraintName("FK_ConstructPlanItemDetailUsers_ConstructPlanItemDetails_ResourceAllocationId")
                            .OnDelete(DeleteBehavior.Cascade),
                        j => 
                        {
                            j.HasKey("UserId", "ResourceAllocationId");
                            j.ToTable("ConstructPlanItemDetailUsers");
                        });

                // Many-to-many with Material
                entity.HasMany(e => e.Materials)
                    .WithMany(m => m.ConstructPlanItemDetails)
                    .UsingEntity<Dictionary<string, object>>(
                        "ConstructPlanItemDetailMaterials",
                        j => j
                            .HasOne<Material>()
                            .WithMany()
                            .HasForeignKey("MaterialId")
                            .HasConstraintName("FK_ConstructPlanItemDetailMaterials_Materials_MaterialId")
                            .OnDelete(DeleteBehavior.Cascade),
                        j => j
                            .HasOne<ConstructPlanItemDetail>()
                            .WithMany()
                            .HasForeignKey("ConstructPlanItemDetailId")
                            .HasConstraintName("FK_ConstructPlanItemDetailMaterials_ConstructPlanItemDetails_ConstructPlanItemDetailId")
                            .OnDelete(DeleteBehavior.Cascade),
                        j => 
                        {
                            j.HasKey("MaterialId", "ConstructPlanItemDetailId");
                            j.ToTable("ConstructPlanItemDetailMaterials");
                        });
            });

            // Add query filters for resource type
            modelBuilder.Entity<ConstructPlanItemDetail>()
                .HasQueryFilter(e => e.ResourceType == ResourceType.MACHINE || e.Vehicles.Any());

            modelBuilder.Entity<ConstructPlanItemDetail>()
                .HasQueryFilter(e => e.ResourceType == ResourceType.HUMAN || e.Users.Any());

            modelBuilder.Entity<ConstructPlanItemDetail>()
                .HasQueryFilter(e => e.ResourceType == ResourceType.MATERIAL || e.Materials.Any());
        }
    }
}
