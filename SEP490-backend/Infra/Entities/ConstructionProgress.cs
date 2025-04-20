using Microsoft.EntityFrameworkCore;
using Sep490_Backend.Infra.Enums;

namespace Sep490_Backend.Infra.Entities
{
    public class ConstructionProgress : CommonEntity
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public int PlanId { get; set; }

        // Navigation properties
        public virtual Project Project { get; set; }
        public virtual ConstructionPlan ConstructionPlan { get; set; }
        public virtual ICollection<ConstructionProgressItem> ProgressItems { get; set; } = new List<ConstructionProgressItem>();
    }

    public class ConstructionProgressItem : CommonEntity
    {
        public int Id { get; set; }
        public int ProgressId { get; set; }
        public string WorkCode { get; set; }
        public string Index { get; set; }
        public string? ParentIndex { get; set; }
        public string WorkName { get; set; }
        public string Unit { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public int Progress { get; set; } = 0; // 0-100 percent
        public ProgressStatusEnum Status { get; set; } = ProgressStatusEnum.NotStarted;
        public DateTime PlanStartDate { get; set; }
        public DateTime PlanEndDate { get; set; }
        public DateTime? ActualStartDate { get; set; }
        public DateTime? ActualEndDate { get; set; }
        public Dictionary<string, string> ItemRelations { get; set; } = new Dictionary<string, string>();

        // Navigation properties
        public virtual ConstructionProgress ConstructionProgress { get; set; }
        public virtual ICollection<ConstructionProgressItemDetail> Details { get; set; } = new List<ConstructionProgressItemDetail>();
    }

    public class ConstructionProgressItemDetail : CommonEntity
    {
        public int Id { get; set; }
        public int ProgressItemId { get; set; }
        public string WorkCode { get; set; }
        public ResourceType ResourceType { get; set; }
        public int Quantity { get; set; }
        public string? Unit { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total { get; set; }
        public int? ResourceId { get; set; }

        // Navigation properties
        public virtual ConstructionProgressItem ProgressItem { get; set; }
    }

    public static class ConstructionProgressConfiguration
    {
        public static void Config(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ConstructionProgress>(entity =>
            {
                entity.ToTable("ConstructionProgresses");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.ProjectId).IsRequired();
                entity.Property(e => e.PlanId).IsRequired();
                
                entity.Property(e => e.CreatedAt).HasColumnType("timestamp without time zone");
                entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone");

                entity.HasIndex(e => e.ProjectId);
                entity.HasIndex(e => e.PlanId);

                // Relationships
                entity.HasOne(e => e.Project)
                    .WithMany()
                    .HasForeignKey(e => e.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.ConstructionPlan)
                    .WithMany()
                    .HasForeignKey(e => e.PlanId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.ProgressItems)
                    .WithOne(pi => pi.ConstructionProgress)
                    .HasForeignKey(pi => pi.ProgressId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ConstructionProgressItem>(entity =>
            {
                entity.ToTable("ConstructionProgressItems");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.ProgressId).IsRequired();
                
                entity.Property(e => e.WorkCode)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.Index)
                    .IsRequired()
                    .HasMaxLength(50);

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

                entity.Property(e => e.Progress)
                    .HasDefaultValue(0);

                entity.Property(e => e.Status)
                    .HasDefaultValue(ProgressStatusEnum.NotStarted);

                entity.Property(e => e.PlanStartDate)
                    .IsRequired()
                    .HasColumnType("timestamp without time zone");

                entity.Property(e => e.PlanEndDate)
                    .IsRequired()
                    .HasColumnType("timestamp without time zone");

                entity.Property(e => e.ActualStartDate)
                    .HasColumnType("timestamp without time zone");

                entity.Property(e => e.ActualEndDate)
                    .HasColumnType("timestamp without time zone");

                entity.Property(e => e.ItemRelations)
                    .HasColumnType("jsonb");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp without time zone");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("timestamp without time zone");

                entity.HasIndex(e => e.ProgressId);
                entity.HasIndex(e => e.WorkCode);
                entity.HasIndex(e => e.Status);

                // Create a unique index that scopes Index to ProgressId
                entity.HasIndex(e => new { e.ProgressId, e.Index }).HasFilter("\"Deleted\" = false").IsUnique();

                // Relationships
                entity.HasOne(e => e.ConstructionProgress)
                    .WithMany(cp => cp.ProgressItems)
                    .HasForeignKey(e => e.ProgressId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.Details)
                    .WithOne(d => d.ProgressItem)
                    .HasForeignKey(d => d.ProgressItemId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ConstructionProgressItemDetail>(entity =>
            {
                entity.ToTable("ConstructionProgressItemDetails");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.ProgressItemId)
                    .IsRequired();

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

                entity.HasIndex(e => e.ProgressItemId);
                entity.HasIndex(e => e.ResourceType);
                entity.HasIndex(e => e.ResourceId);

                // Relationships
                entity.HasOne(e => e.ProgressItem)
                    .WithMany(pi => pi.Details)
                    .HasForeignKey(e => e.ProgressItemId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
} 