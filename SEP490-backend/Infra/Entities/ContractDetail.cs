using Microsoft.EntityFrameworkCore;

namespace Sep490_Backend.Infra.Entities
{
    public class ContractDetail : CommonEntity
    {
        public string WorkCode { get; set; }
        public string Index { get; set; }
        public int ContractId { get; set; }
        public string? ParentIndex { get; set; }
        public string WorkName { get; set; }
        public string Unit { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total { get; set; }
    }

    public static class ContractDetailConfiguration
    {
        public static void Config(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ContractDetail>(entity =>
            {
                entity.ToTable("ContractDetails");

                entity.HasKey(e => e.WorkCode);

                entity.Property(e => e.WorkCode)
                      .IsRequired()
                      .HasColumnType("text");

                entity.Property(e => e.WorkName)
                      .IsRequired()
                      .HasColumnType("text");

                entity.Property(e => e.Unit)
                      .IsRequired()
                      .HasColumnType("text");

                entity.Property(e => e.Quantity)
                      .HasColumnType("numeric(18,2)");

                entity.Property(e => e.UnitPrice)
                      .HasColumnType("numeric(18,2)");

                entity.Property(e => e.Total)
                      .HasColumnType("numeric(18,2)");

                entity.Property(e => e.CreatedAt)
                      .HasColumnType("timestamp without time zone");

                entity.Property(e => e.UpdatedAt)
                      .HasColumnType("timestamp without time zone");

                entity.Property(e => e.Deleted)
                      .HasDefaultValue(false);
            });
        }
    }

}
