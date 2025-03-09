using Microsoft.EntityFrameworkCore;
using Sep490_Backend.Infra.Enums;
using System.Text.Json;

namespace Sep490_Backend.Infra.Entities
{
    public class Contract : CommonEntity
    {
        public int Id { get; set; }
        public string ContractCode { get; set; }
        public string ContractName { get; set; }
        public int ProjectId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int EstimatedDays { get; set; }
        public ContractStatusEnum Status { get; set; }
        public decimal Tax { get; set; }
        public decimal Total { get; set; }
        public DateTime SignDate { get; set; }
        public JsonDocument? Attachments { get; set; }
    }

    public static class ContractConfiguration
    {
        public static void Config(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Contract>(entity =>
            {
                entity.ToTable("Contracts");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.ContractCode)
                      .IsRequired();// Bắt buộc
                      
                entity.Property(e => e.ContractName)
                      .IsRequired();

                entity.Property(e => e.StartDate)
                      .IsRequired()
                      .HasColumnType("timestamp without time zone");

                entity.Property(e => e.EndDate)
                      .IsRequired()
                      .HasColumnType("timestamp without time zone");

                entity.Property(e => e.SignDate)
                      .IsRequired()
                      .HasColumnType("timestamp without time zone");

                entity.Property(e => e.CreatedAt)
                      .HasColumnType("timestamp without time zone");

                entity.Property(e => e.UpdatedAt)
                      .HasColumnType("timestamp without time zone");

                entity.Property(e => e.Tax)
                      .HasColumnType("numeric(18,2)");
                      
                entity.Property(e => e.Total)
                      .HasColumnType("numeric(18,2)");
                      
                entity.Property(e => e.Attachments)
                      .HasColumnType("jsonb");

            });
        }
    }
}
