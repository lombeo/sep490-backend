using Microsoft.EntityFrameworkCore;

namespace Sep490_Backend.Infra.Entities
{
    public class Vehicle : CommonEntity
    {
        public int Id { get; set; }
        public string LicensePlate { get; set; }
        public string Brand { get; set; }
        public int YearOfManufacture { get; set; }
        public string CountryOfManufacture { get; set; }
        public int VehicleType { get; set; }
        public string ChassisNumber { get; set; }
        public string EngineNumber { get; set; }
        public string Image { get; set; }
        public int Status { get; set; }
        public int Driver { get; set; }
        public string Color { get; set; }
        public string FuelType { get; set; }
        public string Description { get; set; }
        public int FuelTankVolume { get; set; }
        public string FuelUnit { get; set; }
        public string Attachment { get; set; }
        
        // Navigation property
        public virtual User User { get; set; }
    }

    public static class VehicleConfiguration
    {
        public static void Config(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Vehicle>(entity =>
            {
                entity.ToTable("Vehicles");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.LicensePlate)
                      .IsRequired()
                      .HasColumnType("text");

                entity.Property(e => e.Brand)
                      .IsRequired()
                      .HasColumnType("text");

                entity.Property(e => e.CountryOfManufacture)
                      .IsRequired()
                      .HasColumnType("text");

                entity.Property(e => e.ChassisNumber)
                      .IsRequired()
                      .HasColumnType("text");

                entity.Property(e => e.EngineNumber)
                      .IsRequired()
                      .HasColumnType("text");

                entity.Property(e => e.Image)
                      .IsRequired()
                      .HasColumnType("text");

                entity.Property(e => e.Color)
                      .IsRequired()
                      .HasColumnType("text");

                entity.Property(e => e.FuelType)
                      .IsRequired()
                      .HasColumnType("text");

                entity.Property(e => e.Description)
                      .IsRequired()
                      .HasColumnType("text");

                entity.Property(e => e.FuelUnit)
                      .IsRequired()
                      .HasColumnType("text");

                entity.Property(e => e.Attachment)
                      .IsRequired()
                      .HasColumnType("text");


                entity.Property(e => e.CreatedAt)
                      .HasColumnType("timestamp without time zone");

                entity.Property(e => e.UpdatedAt)
                      .HasColumnType("timestamp without time zone");

                entity.Property(e => e.Deleted)
                      .HasDefaultValue(false);

                // Relationships
                entity.HasOne(e => e.User)
                      .WithMany(u => u.Vehicles)
                      .HasForeignKey(e => e.Driver)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }

}
