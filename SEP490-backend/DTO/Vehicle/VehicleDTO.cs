using Sep490_Backend.Infra.Entities;

namespace Sep490_Backend.DTO.Vehicle
{
    public class VehicleSearchDTO : BaseQuery
    {
        public string? LicensePlate { get; set; }
        public string? Brand { get; set; }
        public int? VehicleType { get; set; }
        public int? Status { get; set; }
        public int? Driver { get; set; }
    }

    public class VehicleCreateDTO
    {
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
    }

    public class VehicleUpdateDTO : VehicleCreateDTO
    {
        public int Id { get; set; }
    }

    public class VehicleResponseDTO
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
        public int DriverId { get; set; }
        public string Color { get; set; }
        public string FuelType { get; set; }
        public string Description { get; set; }
        public int FuelTankVolume { get; set; }
        public string FuelUnit { get; set; }
        public string Attachment { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public User Driver { get; set; }
    }
} 