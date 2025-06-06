﻿using Microsoft.EntityFrameworkCore;
using Sep490_Backend.Infra.Enums;
using System.Text.Json;

namespace Sep490_Backend.Infra.Entities
{
    public class Vehicle : CommonEntity
    {
        public int Id { get; set; }
        public string LicensePlate { get; set; }
        public string? Brand { get; set; }
        public int? YearOfManufacture { get; set; }
        public string? CountryOfManufacture { get; set; }
        public string VehicleType { get; set; }
        public string VehicleName { get; set; }
        public string ChassisNumber { get; set; }
        public string EngineNumber { get; set; }
        public VehicleStatus Status { get; set; }
        public int Driver { get; set; }
        public string? Color { get; set; }
        public string FuelType { get; set; }
        public string? Description { get; set; }
        public int FuelTankVolume { get; set; }
        public string FuelUnit { get; set; }
        
        // Navigation property
        public virtual User User { get; set; }
        public virtual ICollection<ConstructPlanItemDetail> ConstructPlanItemDetails { get; set; }
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
                      .IsRequired(false)
                      .HasColumnType("text");

                entity.Property(e => e.CountryOfManufacture)
                      .IsRequired(false)
                      .HasColumnType("text");

                entity.Property(e => e.VehicleType)
                      .IsRequired()
                      .HasColumnType("text");

                entity.Property(e => e.VehicleName)
                      .IsRequired()
                      .HasColumnType("text");

                entity.Property(e => e.ChassisNumber)
                      .IsRequired()
                      .HasColumnType("text");

                entity.Property(e => e.EngineNumber)
                      .IsRequired()
                      .HasColumnType("text");

                entity.Property(e => e.Status)
                      .IsRequired()
                      .HasDefaultValue(VehicleStatus.Unavailable);

                entity.Property(e => e.Color)
                      .IsRequired(false)
                      .HasColumnType("text");

                entity.Property(e => e.FuelType)
                      .IsRequired()
                      .HasColumnType("text");

                entity.Property(e => e.Description)
                      .IsRequired(false)
                      .HasColumnType("text");

                entity.Property(e => e.FuelUnit)
                      .IsRequired()
                      .HasColumnType("text");

                entity.Property(e => e.CreatedAt)
                      .HasColumnType("timestamp without time zone");

                entity.Property(e => e.UpdatedAt)
                      .HasColumnType("timestamp without time zone");

                entity.Property(e => e.Deleted)
                      .HasDefaultValue(false);

                // One-to-one relationship with User
                entity.HasOne(e => e.User)
                      .WithOne(u => u.Vehicle)
                      .HasForeignKey<Vehicle>(e => e.Driver)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }

}
