using Microsoft.EntityFrameworkCore;

namespace Sep490_Backend.Infra.Entities
{
    public class Customer : CommonEntity
    {
        public int Id { get; set; }
        public string CustomerCode { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? TaxCode { get; set; }
        public string? Fax { get; set; }
        public string? Address { get; set; }
        public string? Email { get; set; }
        public string? DirectorName { get; set; }
        public string? Description { get; set; }
        public string? BankAccount { get; set; }
        public string? BankName { get; set; }
    }

    public static class CustomerConfiguration
    {
        public static void Config(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Customer>(entity =>
            {
                entity.ToTable("Customers");
                entity.HasKey(entity => entity.Id);

                //Các trường thuộc Common
                entity.Property(e => e.CreatedAt)
                      .HasColumnType("timestamp without time zone");
                entity.Property(e => e.UpdatedAt)
                      .HasColumnType("timestamp without time zone");

                //Các thuộc tính 
                entity.Property(e => e.CustomerCode)
                      .IsRequired()
                      .HasMaxLength(50);
                entity.Property(e => e.CustomerName)
                      .IsRequired()
                      .HasMaxLength(255);
                entity.Property(e => e.Phone)
                      .HasMaxLength(20);
                entity.Property(e => e.TaxCode)
                      .HasMaxLength(50);
                entity.Property(e => e.Fax)
                      .HasMaxLength(20);
                entity.Property(e => e.Address)
                      .HasColumnType("text");
                entity.Property(e => e.Email)
                      .HasMaxLength(255);
                entity.Property(e => e.DirectorName)
                      .HasMaxLength(255);
                entity.Property(e => e.Description)
                      .HasColumnType("text");
                entity.Property(e => e.BankAccount)
                      .HasMaxLength(50);
                entity.Property(e => e.BankName)
                      .HasMaxLength(255);

            });
        }
    }
}
