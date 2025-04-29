using Microsoft.EntityFrameworkCore;
using Sep490_Backend.DTO;
using Sep490_Backend.DTO.Vehicle;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Infra.Enums;
using Sep490_Backend.Services.CacheService;
using Sep490_Backend.Services.GoogleDriveService;
using Sep490_Backend.Services.HelperService;
using System.Text;
using System.Text.Json;

namespace Sep490_Backend.Services.VehicleService
{
    public class VehicleService : IVehicleService
    {
        private readonly BackendContext _context;
        private readonly ICacheService _cacheService;
        private readonly IHelperService _helperService;
        private readonly IGoogleDriveService _googleDriveService;
        private readonly TimeSpan DEFAULT_CACHE_DURATION = TimeSpan.FromMinutes(15);
        private readonly JsonSerializerOptions DefaultSerializerOptions = new() { PropertyNameCaseInsensitive = true };

        public VehicleService(
            BackendContext context, 
            ICacheService cacheService, 
            IHelperService helperService,
            IGoogleDriveService googleDriveService)
        {
            _context = context;
            _cacheService = cacheService;
            _helperService = helperService;
            _googleDriveService = googleDriveService;
        }

        private string GetVehicleSearchCacheKey(VehicleSearchDTO searchDto)
        {
            return RedisCacheKey.VEHICLE_CACHE_KEY;
        }

        public async Task<Vehicle> GetVehicleById(int id)
        {
            string cacheKey = RedisCacheKey.VEHICLE_CACHE_KEY;
            
            var cachedVehicles = await _cacheService.GetAsync<List<Vehicle>>(cacheKey);
            
            if (cachedVehicles != null)
            {
                var vehicle = cachedVehicles.FirstOrDefault(v => v.Id == id && !v.Deleted);
                if (vehicle != null)
                {
                    return vehicle;
                }
            }
            
            var foundVehicle = await _context.Vehicles
                .Include(v => v.User)
                .FirstOrDefaultAsync(v => v.Id == id && !v.Deleted);

            if (foundVehicle == null)
            {
                throw new KeyNotFoundException(Message.VehicleMessage.NOT_FOUND);
            }

            if (cachedVehicles == null)
            {
                cachedVehicles = await _context.Vehicles
                    .Include(v => v.User)
                    .Where(v => !v.Deleted)
                    .ToListAsync();
                    
                await _cacheService.SetAsync(cacheKey, cachedVehicles, DEFAULT_CACHE_DURATION);
            }
            else if (!cachedVehicles.Any(v => v.Id == id))
            {
                cachedVehicles.Add(foundVehicle);
                await _cacheService.SetAsync(cacheKey, cachedVehicles, DEFAULT_CACHE_DURATION);
            }
            
            return foundVehicle;
        }

        public async Task<List<Vehicle>> GetVehicles(VehicleSearchDTO searchDto)
        {
            string cacheKey = RedisCacheKey.VEHICLE_CACHE_KEY;
            
            var allVehicles = await _cacheService.GetAsync<List<Vehicle>>(cacheKey);
            
            if (allVehicles == null)
            {
                allVehicles = await _context.Vehicles
                    .Include(v => v.User)
                    .Where(v => !v.Deleted)
                    .ToListAsync();
                    
                await _cacheService.SetAsync(cacheKey, allVehicles, DEFAULT_CACHE_DURATION);
            }
            
            var filteredVehicles = allVehicles.AsQueryable();
            
            if (!string.IsNullOrEmpty(searchDto.KeyWord))
            {
                filteredVehicles = filteredVehicles.Where(v => 
                    v.LicensePlate.Contains(searchDto.KeyWord) ||
                    v.Brand.Contains(searchDto.KeyWord) ||
                    v.VehicleType.Contains(searchDto.KeyWord) ||
                    v.VehicleName.Contains(searchDto.KeyWord)
                );
            }

            if (searchDto.Status.HasValue)
            {
                filteredVehicles = filteredVehicles.Where(v => v.Status == searchDto.Status.Value);
            }

            if (searchDto.Driver.HasValue)
            {
                filteredVehicles = filteredVehicles.Where(v => v.Driver == searchDto.Driver.Value);
            }

            var filteredList = filteredVehicles.ToList();
            
            searchDto.Total = filteredList.Count;

            return filteredList
                .OrderByDescending(v => v.UpdatedAt)
                .Skip(searchDto.Skip)
                .Take(searchDto.PageSize)
                .ToList();
        }

        public async Task<Vehicle> CreateVehicle(VehicleCreateDTO vehicleDto, int userId)
        {
            // Authorization check - only Administrator can create vehicles
            if (!_helperService.IsInRole(userId, RoleConstValue.ADMIN))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            
            var existingVehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v => v.LicensePlate == vehicleDto.LicensePlate && !v.Deleted);

            if (existingVehicle != null)
            {
                throw new ArgumentException(Message.VehicleMessage.LICENSE_PLATE_EXISTS);
            }

            var driver = await _context.Users.FirstOrDefaultAsync(u => u.Id == vehicleDto.Driver && !u.Deleted);
            if (driver == null)
            {
                throw new ArgumentException(Message.CommonMessage.NOT_FOUND);
            }

            var vehicle = new Vehicle
            {
                LicensePlate = vehicleDto.LicensePlate,
                Brand = vehicleDto.Brand,
                YearOfManufacture = vehicleDto.YearOfManufacture,
                CountryOfManufacture = vehicleDto.CountryOfManufacture,
                VehicleType = vehicleDto.VehicleType,
                VehicleName = vehicleDto.VehicleName,
                ChassisNumber = vehicleDto.ChassisNumber,
                EngineNumber = vehicleDto.EngineNumber,
                Status = vehicleDto.Status,
                Driver = vehicleDto.Driver,
                Color = vehicleDto.Color,
                FuelType = vehicleDto.FuelType,
                Description = vehicleDto.Description,
                FuelTankVolume = vehicleDto.FuelTankVolume,
                FuelUnit = vehicleDto.FuelUnit,
                Creator = userId,
                Updater = userId
            };

            await _context.Vehicles.AddAsync(vehicle);
            await _context.SaveChangesAsync();

            await InvalidateVehicleCaches();
            
            return vehicle;
        }

        public async Task<Vehicle> UpdateVehicle(VehicleUpdateDTO vehicleDto, int userId)
        {
            // Authorization check - only Administrator can update vehicles
            if (!_helperService.IsInRole(userId, RoleConstValue.ADMIN))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v => v.Id == vehicleDto.Id && !v.Deleted);

            if (vehicle == null)
            {
                throw new KeyNotFoundException(Message.VehicleMessage.NOT_FOUND);
            }

            if (vehicle.LicensePlate != vehicleDto.LicensePlate)
            {
                var existingVehicle = await _context.Vehicles
                    .FirstOrDefaultAsync(v => v.LicensePlate == vehicleDto.LicensePlate && v.Id != vehicleDto.Id && !v.Deleted);

                if (existingVehicle != null)
                {
                    throw new ArgumentException(Message.VehicleMessage.LICENSE_PLATE_EXISTS);
                }
            }

            var driver = await _context.Users.FirstOrDefaultAsync(u => u.Id == vehicleDto.Driver && !u.Deleted);
            if (driver == null)
            {
                throw new ArgumentException(Message.CommonMessage.NOT_FOUND);
            }

            vehicle.LicensePlate = vehicleDto.LicensePlate;
            vehicle.Brand = vehicleDto.Brand;
            vehicle.YearOfManufacture = vehicleDto.YearOfManufacture;
            vehicle.CountryOfManufacture = vehicleDto.CountryOfManufacture;
            vehicle.VehicleType = vehicleDto.VehicleType;
            vehicle.VehicleName = vehicleDto.VehicleName;
            vehicle.ChassisNumber = vehicleDto.ChassisNumber;
            vehicle.EngineNumber = vehicleDto.EngineNumber;
            vehicle.Status = vehicleDto.Status;
            vehicle.Driver = vehicleDto.Driver;
            vehicle.Color = vehicleDto.Color;
            vehicle.FuelType = vehicleDto.FuelType;
            vehicle.Description = vehicleDto.Description;
            vehicle.FuelTankVolume = vehicleDto.FuelTankVolume;
            vehicle.FuelUnit = vehicleDto.FuelUnit;
            vehicle.Updater = userId;

            _context.Vehicles.Update(vehicle);
            await _context.SaveChangesAsync();

            await InvalidateVehicleCaches();
            
            return vehicle;
        }

        public async Task<bool> DeleteVehicle(int id, int userId)
        {
            // Authorization check - only Administrator can delete vehicles
            if (!_helperService.IsInRole(userId, RoleConstValue.ADMIN))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            
            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v => v.Id == id && !v.Deleted);

            if (vehicle == null)
            {
                throw new KeyNotFoundException(Message.VehicleMessage.NOT_FOUND);
            }

            vehicle.Deleted = true;
            vehicle.Updater = userId;

            _context.Vehicles.Update(vehicle);
            await _context.SaveChangesAsync();

            await InvalidateVehicleCaches();
            
            return true;
        }
        
        private async Task InvalidateVehicleCaches(int? specificVehicleId = null)
        {
            await _cacheService.DeleteAsync(RedisCacheKey.VEHICLE_CACHE_KEY);
        }
    }
} 