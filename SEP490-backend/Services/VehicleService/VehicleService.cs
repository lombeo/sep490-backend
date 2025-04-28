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

            // Process image upload
            var imageInfos = new List<AttachmentInfo>();
            if (vehicleDto.ImageFile != null)
            {
                // Validate image file type
                if (!_googleDriveService.IsValidImageFile(vehicleDto.ImageFile.FileName, vehicleDto.ImageFile.ContentType))
                {
                    throw new ArgumentException($"Invalid image file type: {vehicleDto.ImageFile.FileName}. Only JPEG, PNG, GIF, BMP, WebP and TIFF are allowed.");
                }
                
                using (var stream = vehicleDto.ImageFile.OpenReadStream())
                {
                    var uploadResult = await _googleDriveService.UploadFile(
                        stream,
                        vehicleDto.ImageFile.FileName,
                        vehicleDto.ImageFile.ContentType
                    );

                    // Parse Google Drive response to get file ID
                    var fileId = uploadResult.Split("id=").Last().Split("&").First();
                    
                    imageInfos.Add(new AttachmentInfo
                    {
                        Id = fileId,
                        Name = vehicleDto.ImageFile.FileName,
                        WebViewLink = $"https://drive.google.com/file/d/{fileId}/view",
                        WebContentLink = uploadResult,
                        MimeType = vehicleDto.ImageFile.ContentType
                    });
                }
            }

            // Process attachments
            var attachmentInfos = new List<AttachmentInfo>();
            
            // Single attachment file
            if (vehicleDto.AttachmentFile != null)
            {
                using (var stream = vehicleDto.AttachmentFile.OpenReadStream())
                {
                    var uploadResult = await _googleDriveService.UploadFile(
                        stream,
                        vehicleDto.AttachmentFile.FileName,
                        vehicleDto.AttachmentFile.ContentType
                    );

                    // Parse Google Drive response to get file ID
                    var fileId = uploadResult.Split("id=").Last().Split("&").First();
                    
                    attachmentInfos.Add(new AttachmentInfo
                    {
                        Id = fileId,
                        Name = vehicleDto.AttachmentFile.FileName,
                        WebViewLink = $"https://drive.google.com/file/d/{fileId}/view",
                        WebContentLink = uploadResult,
                        MimeType = vehicleDto.AttachmentFile.ContentType
                    });
                }
            }
            
            // Multiple attachment files
            if (vehicleDto.AttachmentFiles != null && vehicleDto.AttachmentFiles.Any())
            {
                foreach (var file in vehicleDto.AttachmentFiles)
                {
                    using (var stream = file.OpenReadStream())
                    {
                        var uploadResult = await _googleDriveService.UploadFile(
                            stream,
                            file.FileName,
                            file.ContentType
                        );

                        // Parse Google Drive response to get file ID
                        var fileId = uploadResult.Split("id=").Last().Split("&").First();
                        
                        attachmentInfos.Add(new AttachmentInfo
                        {
                            Id = fileId,
                            Name = file.FileName,
                            WebViewLink = $"https://drive.google.com/file/d/{fileId}/view",
                            WebContentLink = uploadResult,
                            MimeType = file.ContentType
                        });
                    }
                }
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
                Image = imageInfos.Any() 
                    ? JsonDocument.Parse(JsonSerializer.Serialize(imageInfos, DefaultSerializerOptions))
                    : JsonDocument.Parse(JsonSerializer.Serialize(new List<AttachmentInfo>(), DefaultSerializerOptions)),
                Status = vehicleDto.Status,
                Driver = vehicleDto.Driver,
                Color = vehicleDto.Color,
                FuelType = vehicleDto.FuelType,
                Description = vehicleDto.Description,
                FuelTankVolume = vehicleDto.FuelTankVolume,
                FuelUnit = vehicleDto.FuelUnit,
                Attachment = attachmentInfos.Any() 
                    ? JsonDocument.Parse(JsonSerializer.Serialize(attachmentInfos, DefaultSerializerOptions))
                    : JsonDocument.Parse(JsonSerializer.Serialize(new List<AttachmentInfo>(), DefaultSerializerOptions)),
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

            // Process image update
            var imageInfos = new List<AttachmentInfo>();
            
            // Get existing images
            if (vehicle.Image != null)
            {
                imageInfos = JsonSerializer.Deserialize<List<AttachmentInfo>>(vehicle.Image.RootElement.ToString(), DefaultSerializerOptions) ?? new List<AttachmentInfo>();
                
                // Delete old images if there are new ones
                if (vehicleDto.ImageFile != null)
                {
                    try
                    {
                        var linksToDelete = imageInfos.Select(a => a.WebContentLink).ToList();
                        await _googleDriveService.DeleteFilesByLinks(linksToDelete);
                        imageInfos.Clear();
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue with upload
                        Console.WriteLine($"Failed to delete old images: {ex.Message}");
                    }
                }
            }
            
            // Upload new image if provided
            if (vehicleDto.ImageFile != null)
            {
                // Validate image file type
                if (!_googleDriveService.IsValidImageFile(vehicleDto.ImageFile.FileName, vehicleDto.ImageFile.ContentType))
                {
                    throw new ArgumentException($"Invalid image file type: {vehicleDto.ImageFile.FileName}. Only JPEG, PNG, GIF, BMP, WebP and TIFF are allowed.");
                }
                
                using (var stream = vehicleDto.ImageFile.OpenReadStream())
                {
                    var uploadResult = await _googleDriveService.UploadFile(
                        stream,
                        vehicleDto.ImageFile.FileName,
                        vehicleDto.ImageFile.ContentType
                    );

                    // Parse Google Drive response to get file ID
                    var fileId = uploadResult.Split("id=").Last().Split("&").First();
                    
                    imageInfos.Add(new AttachmentInfo
                    {
                        Id = fileId,
                        Name = vehicleDto.ImageFile.FileName,
                        WebViewLink = $"https://drive.google.com/file/d/{fileId}/view",
                        WebContentLink = uploadResult,
                        MimeType = vehicleDto.ImageFile.ContentType
                    });
                }
            }
            // If no new image but has existing images passed
            else if (vehicleDto.ExistingImages != null && vehicleDto.ExistingImages.Any())
            {
                // Clear existing and use the provided ones
                imageInfos.Clear();
                vehicleDto.ExistingImages.ForEach(url => 
                {
                    imageInfos.Add(new AttachmentInfo
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = "Existing Image",
                        WebViewLink = url,
                        WebContentLink = url
                    });
                });
            }

            // Process attachments update
            var attachmentInfos = new List<AttachmentInfo>();
            
            // Get existing attachments
            if (vehicle.Attachment != null)
            {
                attachmentInfos = JsonSerializer.Deserialize<List<AttachmentInfo>>(vehicle.Attachment.RootElement.ToString(), DefaultSerializerOptions) ?? new List<AttachmentInfo>();
                
                // Delete old attachments if there are new ones
                if (vehicleDto.AttachmentFile != null || (vehicleDto.AttachmentFiles != null && vehicleDto.AttachmentFiles.Any()))
                {
                    try
                    {
                        var linksToDelete = attachmentInfos.Select(a => a.WebContentLink).ToList();
                        await _googleDriveService.DeleteFilesByLinks(linksToDelete);
                        attachmentInfos.Clear();
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue with upload
                        Console.WriteLine($"Failed to delete old attachments: {ex.Message}");
                    }
                }
            }
            
            // Single attachment file
            if (vehicleDto.AttachmentFile != null)
            {
                using (var stream = vehicleDto.AttachmentFile.OpenReadStream())
                {
                    var uploadResult = await _googleDriveService.UploadFile(
                        stream,
                        vehicleDto.AttachmentFile.FileName,
                        vehicleDto.AttachmentFile.ContentType
                    );

                    // Parse Google Drive response to get file ID
                    var fileId = uploadResult.Split("id=").Last().Split("&").First();
                    
                    attachmentInfos.Add(new AttachmentInfo
                    {
                        Id = fileId,
                        Name = vehicleDto.AttachmentFile.FileName,
                        WebViewLink = $"https://drive.google.com/file/d/{fileId}/view",
                        WebContentLink = uploadResult,
                        MimeType = vehicleDto.AttachmentFile.ContentType
                    });
                }
            }
            
            // Multiple attachment files
            if (vehicleDto.AttachmentFiles != null && vehicleDto.AttachmentFiles.Any())
            {
                foreach (var file in vehicleDto.AttachmentFiles)
                {
                    using (var stream = file.OpenReadStream())
                    {
                        var uploadResult = await _googleDriveService.UploadFile(
                            stream,
                            file.FileName,
                            file.ContentType
                        );

                        // Parse Google Drive response to get file ID
                        var fileId = uploadResult.Split("id=").Last().Split("&").First();
                        
                        attachmentInfos.Add(new AttachmentInfo
                        {
                            Id = fileId,
                            Name = file.FileName,
                            WebViewLink = $"https://drive.google.com/file/d/{fileId}/view",
                            WebContentLink = uploadResult,
                            MimeType = file.ContentType
                        });
                    }
                }
            }
            // If no new attachments but has existing attachments passed
            else if (vehicleDto.ExistingAttachments != null && vehicleDto.ExistingAttachments.Any())
            {
                // Clear existing and use the provided ones
                attachmentInfos.Clear();
                vehicleDto.ExistingAttachments.ForEach(url => 
                {
                    attachmentInfos.Add(new AttachmentInfo
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = "Existing Attachment",
                        WebViewLink = url,
                        WebContentLink = url
                    });
                });
            }

            vehicle.LicensePlate = vehicleDto.LicensePlate;
            vehicle.Brand = vehicleDto.Brand;
            vehicle.YearOfManufacture = vehicleDto.YearOfManufacture;
            vehicle.CountryOfManufacture = vehicleDto.CountryOfManufacture;
            vehicle.VehicleType = vehicleDto.VehicleType;
            vehicle.VehicleName = vehicleDto.VehicleName;
            vehicle.ChassisNumber = vehicleDto.ChassisNumber;
            vehicle.EngineNumber = vehicleDto.EngineNumber;
            vehicle.Image = imageInfos.Any() 
                ? JsonDocument.Parse(JsonSerializer.Serialize(imageInfos, DefaultSerializerOptions))
                : vehicle.Image;
            vehicle.Status = vehicleDto.Status;
            vehicle.Driver = vehicleDto.Driver;
            vehicle.Color = vehicleDto.Color;
            vehicle.FuelType = vehicleDto.FuelType;
            vehicle.Description = vehicleDto.Description;
            vehicle.FuelTankVolume = vehicleDto.FuelTankVolume;
            vehicle.FuelUnit = vehicleDto.FuelUnit;
            vehicle.Attachment = attachmentInfos.Any() 
                ? JsonDocument.Parse(JsonSerializer.Serialize(attachmentInfos, DefaultSerializerOptions))
                : vehicle.Attachment;
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

            // Delete files from Google Drive
            if (vehicle.Image != null)
            {
                try
                {
                    var images = JsonSerializer.Deserialize<List<AttachmentInfo>>(vehicle.Image.RootElement.ToString(), DefaultSerializerOptions);
                    if (images != null && images.Any())
                    {
                        var linksToDelete = images.Select(a => a.WebContentLink).ToList();
                        await _googleDriveService.DeleteFilesByLinks(linksToDelete);
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue with deletion
                    Console.WriteLine($"Failed to delete images from Google Drive: {ex.Message}");
                }
            }
            
            if (vehicle.Attachment != null)
            {
                try
                {
                    var attachments = JsonSerializer.Deserialize<List<AttachmentInfo>>(vehicle.Attachment.RootElement.ToString(), DefaultSerializerOptions);
                    if (attachments != null && attachments.Any())
                    {
                        var linksToDelete = attachments.Select(a => a.WebContentLink).ToList();
                        await _googleDriveService.DeleteFilesByLinks(linksToDelete);
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue with deletion
                    Console.WriteLine($"Failed to delete attachments from Google Drive: {ex.Message}");
                }
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