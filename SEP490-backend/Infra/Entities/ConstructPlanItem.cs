using Microsoft.EntityFrameworkCore;

namespace Sep490_Backend.Infra.Entities
{
    public class ConstructPlanItem : CommonEntity
    {
        public string WorkCode { get; set; } //unique
        public string Index { get; set; }
        public int PlanId { get; set; } //ConstrucionPlan
        public string? ParentIndex { get; set; } //ConstructPlanItem
        public string WorkName { get; set; }
        public string Unit { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public Dictionary<string, string> ItemRelations { get; set; } //index, enum

        // Navigation properties
        public virtual ConstructionPlan ConstructionPlan { get; set; }
        public virtual ICollection<ConstructionTeam> ConstructionTeams { get; set; }
        public virtual ConstructPlanItem ParentItem { get; set; }
        public virtual ICollection<ConstructPlanItem> ChildItems { get; set; }
        public virtual ICollection<ConstructPlanItemDetail> ConstructPlanItemDetails { get; set; }
    }

    public static class ConstructPlanItemConfiguration
    {
        public static void Config(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ConstructPlanItem>(entity =>
            {
                entity.ToTable("ConstructPlanItems");
                entity.HasKey(e => e.WorkCode);

                entity.Property(e => e.Index)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.PlanId)
                    .IsRequired();

                entity.Property(e => e.ParentIndex)
                    .HasMaxLength(50);

                entity.Property(e => e.WorkName)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(e => e.Unit)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.Quantity)
                    .HasColumnType("numeric(18,2)");

                entity.Property(e => e.UnitPrice)
                    .HasColumnType("numeric(18,2)");

                entity.Property(e => e.TotalPrice)
                    .HasColumnType("numeric(18,2)");

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

                entity.HasIndex(e => e.PlanId);
                entity.HasIndex(e => e.StartDate);
                entity.HasIndex(e => e.EndDate);

                // Create a unique index that scopes Index to PlanId
                entity.HasIndex(e => new { e.PlanId, e.Index }).IsUnique();

                // Relationships
                entity.HasOne(e => e.ConstructionPlan)
                    .WithMany(cp => cp.ConstructPlanItems)
                    .HasForeignKey(e => e.PlanId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Many-to-many relationship with ConstructionTeam
                entity.HasMany(e => e.ConstructionTeams)
                    .WithMany(ct => ct.ConstructPlanItems)
                    .UsingEntity<Dictionary<string, object>>(
                        "ConstructionTeamPlanItems",
                        j => j
                            .HasOne<ConstructionTeam>()
                            .WithMany()
                            .HasForeignKey("ConstructionTeamId")
                            .HasConstraintName("FK_ConstructionTeamPlanItems_ConstructionTeams_ConstructionTeamId")
                            .OnDelete(DeleteBehavior.Cascade),
                        j => j
                            .HasOne<ConstructPlanItem>()
                            .WithMany()
                            .HasForeignKey("ConstructPlanItemWorkCode")
                            .HasConstraintName("FK_ConstructionTeamPlanItems_ConstructPlanItems_ConstructPlanItemWorkCode")
                            .OnDelete(DeleteBehavior.Cascade),
                        j => 
                        {
                            j.HasKey("ConstructionTeamId", "ConstructPlanItemWorkCode");
                            j.ToTable("ConstructionTeamPlanItems");
                        });

                // Modified: One-to-many relationship with child items using composite key
                entity.HasMany(e => e.ChildItems)
                    .WithOne(e => e.ParentItem)
                    .HasForeignKey("ParentIndex", "PlanId")
                    .HasPrincipalKey("Index", "PlanId")
                    .OnDelete(DeleteBehavior.Restrict);

                // One-to-many relationship with details
                entity.HasMany(e => e.ConstructPlanItemDetails)
                    .WithOne(cpid => cpid.ConstructPlanItem)
                    .HasForeignKey(cpid => cpid.PlanItemId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
