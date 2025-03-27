using Sep490_Backend.DTO.Vehicle;
using Sep490_Backend.Infra.Entities;

namespace Sep490_Backend.Services.VehicleService
{
    public interface IVehicleService
    {
        Task<Vehicle> GetVehicleById(int id);
        Task<List<Vehicle>> GetVehicles(VehicleSearchDTO searchDto);
        Task<Vehicle> CreateVehicle(VehicleCreateDTO vehicleDto, int userId);
        Task<Vehicle> UpdateVehicle(VehicleUpdateDTO vehicleDto, int userId);
        Task<bool> DeleteVehicle(int id, int userId);
    }
} 