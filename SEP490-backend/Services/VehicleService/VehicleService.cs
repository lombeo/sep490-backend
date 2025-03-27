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

        private string GetVehicleByIdCacheKey(int id) => string.Format(RedisCacheKey.VEHICLE_BY_ID_CACHE_KEY, id);
        private string GetVehicleSearchCacheKey(VehicleSearchDTO searchDto)
        {
            var stringBuilder = new StringBuilder(RedisCacheKey.VEHICLE_SEARCH_CACHE_KEY);
            stringBuilder.Append("_");
            
            if (!string.IsNullOrEmpty(searchDto.LicensePlate))
                stringBuilder.Append($"LP_{searchDto.LicensePlate}_");
            
            if (!string.IsNullOrEmpty(searchDto.Brand))
                stringBuilder.Append($"BR_{searchDto.Brand}_");
            
            if (searchDto.VehicleType.HasValue)
                stringBuilder.Append($"VT_{searchDto.VehicleType.Value}_");
            
            if (searchDto.Status.HasValue)
                stringBuilder.Append($"ST_{searchDto.Status.Value}_");
            
            if (searchDto.Driver.HasValue)
                stringBuilder.Append($"DR_{searchDto.Driver.Value}_");
            
            stringBuilder.Append($"PI_{searchDto.PageIndex}_PS_{searchDto.PageSize}");
            
            return stringBuilder.ToString();
        }

        public async Task<Vehicle> GetVehicleById(int id)
        {
            var cacheKey = GetVehicleByIdCacheKey(id);
            
            // Try to get from cache first
            var cachedVehicle = await _cacheService.GetAsync<Vehicle>(cacheKey, true);
            
            if (cachedVehicle != null)
            {
                _logger.LogInformation($"Cache hit for vehicle ID {id}");
                return cachedVehicle;
            }
            
            _logger.LogInformation($"Cache miss for vehicle ID {id}, fetching from database");
            
            // If not in cache, get from database
            var vehicle = await _context.Vehicles
                .Include(v => v.User)
                .FirstOrDefaultAsync(v => v.Id == id && !v.Deleted);

            if (vehicle == null)
            {
                throw new KeyNotFoundException($"Vehicle with ID {id} not found");
            }

            // Cache the result
            await _cacheService.SetAsync(cacheKey, vehicle, DEFAULT_CACHE_DURATION, true);
            
            return vehicle;
        }

        public async Task<List<Vehicle>> GetVehicles(VehicleSearchDTO searchDto)
        {
            var cacheKey = GetVehicleSearchCacheKey(searchDto);
            
            // Try to get from cache first
            var cachedVehicles = await _cacheService.GetAsync<List<Vehicle>>(cacheKey);
            
            if (cachedVehicles != null && cachedVehicles.Count > 0)
            {
                _logger.LogInformation($"Cache hit for vehicles search: {cacheKey}");
                return cachedVehicles;
            }
            
            _logger.LogInformation($"Cache miss for vehicles search: {cacheKey}, fetching from database");
            
            // If not in cache, get from database
            var query = _context.Vehicles
                .Include(v => v.User)
                .Where(v => !v.Deleted)
                .AsQueryable();

            // Apply filters if provided
            if (!string.IsNullOrEmpty(searchDto.LicensePlate))
            {
                query = query.Where(v => v.LicensePlate.Contains(searchDto.LicensePlate));
            }

            if (!string.IsNullOrEmpty(searchDto.Brand))
            {
                query = query.Where(v => v.Brand.Contains(searchDto.Brand));
            }

            if (searchDto.VehicleType.HasValue)
            {
                query = query.Where(v => v.VehicleType == searchDto.VehicleType.Value);
            }

            if (searchDto.Status.HasValue)
            {
                query = query.Where(v => v.Status == searchDto.Status.Value);
            }

            if (searchDto.Driver.HasValue)
            {
                query = query.Where(v => v.Driver == searchDto.Driver.Value);
            }

            // Count total records for pagination
            searchDto.Total = await query.CountAsync();

            // Apply pagination
            var vehicles = await query
                .OrderByDescending(v => v.UpdatedAt)
                .Skip(searchDto.Skip)
                .Take(searchDto.PageSize)
                .ToListAsync();

            // Cache the result
            await _cacheService.SetAsync(cacheKey, vehicles, DEFAULT_CACHE_DURATION);
            
            return vehicles;
        }

        public async Task<Vehicle> CreateVehicle(VehicleCreateDTO vehicleDto, int userId)
        {
            // Check for duplicate license plate
            var existingVehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v => v.LicensePlate == vehicleDto.LicensePlate && !v.Deleted);

            if (existingVehicle != null)
            {
                throw new ArgumentException($"Vehicle with license plate {vehicleDto.LicensePlate} already exists");
            }

            // Check if the driver exists
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

            // Invalidate relevant caches
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

            // Check for duplicate license plate if changed
            if (vehicle.LicensePlate != vehicleDto.LicensePlate)
            {
                var existingVehicle = await _context.Vehicles
                    .FirstOrDefaultAsync(v => v.LicensePlate == vehicleDto.LicensePlate && v.Id != vehicleDto.Id && !v.Deleted);

                if (existingVehicle != null)
                {
                    throw new ArgumentException($"Vehicle with license plate {vehicleDto.LicensePlate} already exists");
                }
            }

            // Check if the driver exists
            var driver = await _context.Users.FirstOrDefaultAsync(u => u.Id == vehicleDto.Driver && !u.Deleted);
            if (driver == null)
            {
                throw new ArgumentException($"Driver with ID {vehicleDto.Driver} not found");
            }

            // Update vehicle properties
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

            // Invalidate relevant caches
            await InvalidateVehicleCaches(vehicle.Id);
            
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

            // Soft delete
            vehicle.Deleted = true;
            vehicle.Updater = userId;

            _context.Vehicles.Update(vehicle);
            await _context.SaveChangesAsync();

            // Invalidate relevant caches
            await InvalidateVehicleCaches(id);
            
            return true;
        }
        
        private async Task InvalidateVehicleCaches(int? specificVehicleId = null)
        {
            var keysToInvalidate = new List<string>();
            
            // Invalidate general vehicle cache
            keysToInvalidate.Add(RedisCacheKey.VEHICLE_CACHE_KEY);
            
            // If a specific vehicle ID is provided, invalidate its cache
            if (specificVehicleId.HasValue)
            {
                keysToInvalidate.Add(GetVehicleByIdCacheKey(specificVehicleId.Value));
            }
            
            // Always invalidate search caches as they may contain the affected vehicle
            keysToInvalidate.Add(RedisCacheKey.VEHICLE_SEARCH_CACHE_KEY);
            
            foreach (var key in keysToInvalidate)
            {
                await _cacheService.DeleteAsync(key);
                _logger.LogInformation($"Invalidated cache with key pattern: {key}");
            }
        }
    }
} 