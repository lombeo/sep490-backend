using Microsoft.EntityFrameworkCore;
using Sep490_Backend.Infra.Enums;

namespace Sep490_Backend.Infra.Entities
{
    public class ResourceInventory : CommonEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int? ResourceId { get; set; }
        public int? ProjectId { get; set; }
        public ResourceType ResourceType { get; set; }
        public int Quantity { get; set; }
        public string Unit { get; set; }
        public bool Status { get; set; }
    }

    public static class ResourceInventoryConfiguration
    {
        public static void Config(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ResourceInventory>(entity =>
            {
                entity.ToTable("ResourceInventory");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(e => e.Description)
                    .HasColumnType("text");

                entity.Property(e => e.ResourceId)
                    .IsRequired(false);

                entity.Property(e => e.ProjectId)
                    .IsRequired(false);

                entity.Property(e => e.ResourceType)
                    .IsRequired();

                entity.Property(e => e.Quantity)
                    .IsRequired();

                entity.Property(e => e.Unit)
                    .HasMaxLength(50);

                entity.Property(e => e.Status)
                    .IsRequired();

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp without time zone");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("timestamp without time zone");

                entity.HasIndex(e => e.ResourceType);
                entity.HasIndex(e => e.Name);
            });
        }
    }
} 