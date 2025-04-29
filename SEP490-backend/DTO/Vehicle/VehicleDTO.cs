using Sep490_Backend.DTO;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Infra.Enums;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace Sep490_Backend.DTO.Vehicle
{
    public class VehicleSearchDTO : BaseQuery
    {
        public string? KeyWord { get; set; }
        public VehicleStatus? Status { get; set; }
        public int? Driver { get; set; }
    }

    public class VehicleCreateDTO
    {
        [Required(ErrorMessage = "License plate is required for construction vehicle tracking")]
        public string LicensePlate { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Brand is required for construction vehicle management")]
        public string Brand { get; set; } = string.Empty;
        
        public int YearOfManufacture { get; set; }
        
        [Required(ErrorMessage = "Country of manufacture is required for vehicle documentation")]
        public string CountryOfManufacture { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Vehicle type is required for vehicle categorization")]
        public string VehicleType { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Vehicle name is required for vehicle identification")]
        public string VehicleName { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Chassis number is required for vehicle identification")]
        public string ChassisNumber { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Engine number is required for vehicle registration")]
        public string EngineNumber { get; set; } = string.Empty;
        
        public VehicleStatus Status { get; set; } = VehicleStatus.Unavailable;
        public int Driver { get; set; }
        
        [Required(ErrorMessage = "Color is required for vehicle identification")]
        public string Color { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Fuel type is required for vehicle maintenance planning")]
        public string FuelType { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Description is required for vehicle management")]
        public string Description { get; set; } = string.Empty;
        
        public int FuelTankVolume { get; set; }
        
        [Required(ErrorMessage = "Fuel unit is required for consumption tracking")]
        public string FuelUnit { get; set; } = string.Empty;
    }

    public class VehicleUpdateDTO : VehicleCreateDTO
    {
        public int Id { get; set; }
    }

    public class VehicleResponseDTO
    {
        public int Id { get; set; }
        public string LicensePlate { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public int YearOfManufacture { get; set; }
        public string CountryOfManufacture { get; set; } = string.Empty;
        public string VehicleType { get; set; } = string.Empty;
        public string VehicleName { get; set; } = string.Empty;
        public string ChassisNumber { get; set; } = string.Empty;
        public string EngineNumber { get; set; } = string.Empty;
        public VehicleStatus Status { get; set; }
        public int DriverId { get; set; }
        public string Color { get; set; } = string.Empty;
        public string FuelType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int FuelTankVolume { get; set; }
        public string FuelUnit { get; set; } = string.Empty;
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public User? Driver { get; set; }
    }
} 