using Microsoft.EntityFrameworkCore;
using Sep490_Backend.DTO.Vehicle;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Services.CacheService;
using System.Text;

namespace Sep490_Backend.Services.VehicleService
{
    public class VehicleService : IVehicleService
    {
        private readonly BackendContext _context;
        private readonly ICacheService _cacheService;
        private readonly ILogger<VehicleService> _logger;
        private readonly TimeSpan DEFAULT_CACHE_DURATION = TimeSpan.FromMinutes(15);

        public VehicleService(BackendContext context, ICacheService cacheService, ILogger<VehicleService> logger)
        {
            _context = context;
            _cacheService = cacheService;
            _logger = logger;
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
                _logger.LogInformation($"Cache hit for all vehicles, finding vehicle ID {id}");
                var vehicle = cachedVehicles.FirstOrDefault(v => v.Id == id && !v.Deleted);
                if (vehicle != null)
                {
                    return vehicle;
                }
            }
            
            _logger.LogInformation($"Cache miss for vehicle ID {id}, fetching from database");
            
            var foundVehicle = await _context.Vehicles
                .Include(v => v.User)
                .FirstOrDefaultAsync(v => v.Id == id && !v.Deleted);

            if (foundVehicle == null)
            {
                throw new KeyNotFoundException($"Vehicle with ID {id} not found");
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
                _logger.LogInformation($"Cache miss for all vehicles, fetching from database");
                
                allVehicles = await _context.Vehicles
                    .Include(v => v.User)
                    .Where(v => !v.Deleted)
                    .ToListAsync();
                    
                await _cacheService.SetAsync(cacheKey, allVehicles, DEFAULT_CACHE_DURATION);
            }
            else
            {
                _logger.LogInformation($"Cache hit for all vehicles");
            }
            
            var filteredVehicles = allVehicles.AsQueryable();
            
            if (!string.IsNullOrEmpty(searchDto.LicensePlate))
            {
                filteredVehicles = filteredVehicles.Where(v => v.LicensePlate.Contains(searchDto.LicensePlate));
            }

            if (!string.IsNullOrEmpty(searchDto.Brand))
            {
                filteredVehicles = filteredVehicles.Where(v => v.Brand.Contains(searchDto.Brand));
            }

            if (searchDto.VehicleType.HasValue)
            {
                filteredVehicles = filteredVehicles.Where(v => v.VehicleType == searchDto.VehicleType.Value);
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
            var existingVehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v => v.LicensePlate == vehicleDto.LicensePlate && !v.Deleted);

            if (existingVehicle != null)
            {
                throw new ArgumentException($"Vehicle with license plate {vehicleDto.LicensePlate} already exists");
            }

            var driver = await _context.Users.FirstOrDefaultAsync(u => u.Id == vehicleDto.Driver && !u.Deleted);
            if (driver == null)
            {
                throw new ArgumentException($"Driver with ID {vehicleDto.Driver} not found");
            }

            var vehicle = new Vehicle
            {
                LicensePlate = vehicleDto.LicensePlate,
                Brand = vehicleDto.Brand,
                YearOfManufacture = vehicleDto.YearOfManufacture,
                CountryOfManufacture = vehicleDto.CountryOfManufacture,
                VehicleType = vehicleDto.VehicleType,
                ChassisNumber = vehicleDto.ChassisNumber,
                EngineNumber = vehicleDto.EngineNumber,
                Image = vehicleDto.Image,
                Status = vehicleDto.Status,
                Driver = vehicleDto.Driver,
                Color = vehicleDto.Color,
                FuelType = vehicleDto.FuelType,
                Description = vehicleDto.Description,
                FuelTankVolume = vehicleDto.FuelTankVolume,
                FuelUnit = vehicleDto.FuelUnit,
                Attachment = vehicleDto.Attachment,
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
            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v => v.Id == vehicleDto.Id && !v.Deleted);

            if (vehicle == null)
            {
                throw new KeyNotFoundException($"Vehicle with ID {vehicleDto.Id} not found");
            }

            if (vehicle.LicensePlate != vehicleDto.LicensePlate)
            {
                var existingVehicle = await _context.Vehicles
                    .FirstOrDefaultAsync(v => v.LicensePlate == vehicleDto.LicensePlate && v.Id != vehicleDto.Id && !v.Deleted);

                if (existingVehicle != null)
                {
                    throw new ArgumentException($"Vehicle with license plate {vehicleDto.LicensePlate} already exists");
                }
            }

            var driver = await _context.Users.FirstOrDefaultAsync(u => u.Id == vehicleDto.Driver && !u.Deleted);
            if (driver == null)
            {
                throw new ArgumentException($"Driver with ID {vehicleDto.Driver} not found");
            }

            vehicle.LicensePlate = vehicleDto.LicensePlate;
            vehicle.Brand = vehicleDto.Brand;
            vehicle.YearOfManufacture = vehicleDto.YearOfManufacture;
            vehicle.CountryOfManufacture = vehicleDto.CountryOfManufacture;
            vehicle.VehicleType = vehicleDto.VehicleType;
            vehicle.ChassisNumber = vehicleDto.ChassisNumber;
            vehicle.EngineNumber = vehicleDto.EngineNumber;
            vehicle.Image = vehicleDto.Image;
            vehicle.Status = vehicleDto.Status;
            vehicle.Driver = vehicleDto.Driver;
            vehicle.Color = vehicleDto.Color;
            vehicle.FuelType = vehicleDto.FuelType;
            vehicle.Description = vehicleDto.Description;
            vehicle.FuelTankVolume = vehicleDto.FuelTankVolume;
            vehicle.FuelUnit = vehicleDto.FuelUnit;
            vehicle.Attachment = vehicleDto.Attachment;
            vehicle.Updater = userId;

            _context.Vehicles.Update(vehicle);
            await _context.SaveChangesAsync();

            await InvalidateVehicleCaches();
            
            return vehicle;
        }

        public async Task<bool> DeleteVehicle(int id, int userId)
        {
            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v => v.Id == id && !v.Deleted);

            if (vehicle == null)
            {
                throw new KeyNotFoundException($"Vehicle with ID {id} not found");
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