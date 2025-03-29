using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NPOI.SS.Formula.Functions;
using Sep490_Backend.DTO;
using Sep490_Backend.DTO.Contract;
using Sep490_Backend.DTO.Project;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Services.CacheService;
using Sep490_Backend.Services.DataService;
using Sep490_Backend.Services.GoogleDriveService;
using Sep490_Backend.Services.HelperService;
using System.Text.Json;

namespace Sep490_Backend.Services.ContractService
{
    public interface IContractService
    {
        Task<ContractDTO> Save(SaveContractDTO model);
        Task<int> Delete(int projectId, int actionBy);
        Task<ContractDTO> Detail(int projectId, int actionBy);
    }

    public class ContractService : IContractService
    {
        private readonly BackendContext _context;
        private readonly ICacheService _cacheService;
        private readonly IDataService _dataService;
        private readonly IHelperService _helpService;
        private readonly IGoogleDriveService _googleDriveService;

        // Remove the local cache key constants and use the ones from RedisCacheKey class
        // private const string CONTRACT_BY_PROJECT_CACHE_KEY = "CONTRACT:PROJECT:{0}"; // Pattern: CONTRACT:PROJECT:projectId
        // private const string CONTRACT_BY_USER_CACHE_KEY = "CONTRACT:USER:{0}"; // Pattern: CONTRACT:USER:userId
        // private const string CONTRACT_DETAIL_BY_USER_CACHE_KEY = "CONTRACT_DETAIL:USER:{0}"; // Pattern: CONTRACT_DETAIL:USER:userId

        public ContractService(BackendContext context, ICacheService cacheService, IDataService dataService, IHelperService helpService, IGoogleDriveService googleDriveService)
        {
            _context = context;
            _cacheService = cacheService;
            _dataService = dataService;
            _helpService = helpService;
            _googleDriveService = googleDriveService;
        }

        // Helper method to clear tracking for specific entities
        private void ClearEntityTracking<T>(IEnumerable<T> entities) where T : class
        {
            foreach (var entity in entities)
            {
                _context.Entry(entity).State = EntityState.Detached;
            }
        }

        public async Task<int> Delete(int projectId, int actionBy)
        {
            // Kiểm tra vai trò người dùng
            if (!_helpService.IsInRole(actionBy, new List<string> { RoleConstValue.BUSINESS_EMPLOYEE, RoleConstValue.EXECUTIVE_BOARD }))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            
            // Tìm Contract cần xóa theo ProjectId
            var data = await _context.Contracts.FirstOrDefaultAsync(t => t.ProjectId == projectId && !t.Deleted);
            if (data == null)
            {
                throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
            }

            // Kiểm tra xem người dùng có phải là người tạo Project chứa Contract này không
            var projectCreator = await _context.ProjectUsers
                .FirstOrDefaultAsync(pu => pu.ProjectId == projectId && pu.IsCreator && !pu.Deleted);
                
            if (projectCreator == null || projectCreator.UserId != actionBy)
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            
            // Xóa mềm Contract
            data.Deleted = true;
            data.UpdatedAt = DateTime.UtcNow;
            data.Updater = actionBy;
            _context.Update(data);
            
            // Xóa mềm tất cả ContractDetail liên quan
            var contractDetail = await _context.ContractDetails
                .Where(t => !t.Deleted && t.ContractId == data.Id)
                .ToListAsync();
                
            foreach (var item in contractDetail)
            {
                item.Deleted = true;
                item.UpdatedAt = DateTime.UtcNow;
                item.Updater = actionBy;
                _context.Update(item);
            }

            await _context.SaveChangesAsync();

            // Xóa cache liên quan
            _ = _cacheService.DeleteAsync(new List<string>
            {
                RedisCacheKey.CONTRACT_CACHE_KEY,
                RedisCacheKey.CONTRACT_DETAIL_CACHE_KEY
            });
            
            // Xóa cache theo project
            string projectCacheKey = string.Format(RedisCacheKey.CONTRACT_BY_PROJECT_CACHE_KEY, projectId);
            _ = _cacheService.DeleteAsync(projectCacheKey);
            
            // Xóa cache của người dùng liên quan đến project
            var projectUsers = await _context.ProjectUsers
                .Where(pu => pu.ProjectId == projectId && !pu.Deleted)
                .ToListAsync();
                
            foreach (var pu in projectUsers)
            {
                string userContractCacheKey = string.Format(RedisCacheKey.CONTRACT_BY_USER_CACHE_KEY, pu.UserId);
                string userContractDetailCacheKey = string.Format(RedisCacheKey.CONTRACT_DETAIL_BY_USER_CACHE_KEY, pu.UserId);
                _ = _cacheService.DeleteAsync(userContractCacheKey);
                _ = _cacheService.DeleteAsync(userContractDetailCacheKey);
            }

            return data.Id;
        }

        public async Task<ContractDTO> Detail(int projectId, int actionBy)
        {
            if (!_helpService.IsInRole(actionBy, new List<string> { RoleConstValue.BUSINESS_EMPLOYEE, RoleConstValue.EXECUTIVE_BOARD }))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            
            // Verificar si el usuario es Executive Board
            var user = StaticVariable.UserMemory.FirstOrDefault(u => u.Id == actionBy);
            bool isExecutiveBoard = user != null && user.Role == RoleConstValue.EXECUTIVE_BOARD;
            
            // Kiểm tra trong cache của người dùng trước
            string userContractCacheKey = string.Format(RedisCacheKey.CONTRACT_BY_USER_CACHE_KEY, actionBy);
            var userContracts = await _cacheService.GetAsync<List<ContractDTO>>(userContractCacheKey);
            
            if (userContracts != null)
            {
                // Tìm contract trong cache theo ProjectId thay vì Id
                var contractFromCache = userContracts.FirstOrDefault(c => c.Project.Id == projectId);
                if (contractFromCache != null)
                {
                    return contractFromCache;
                }
            }
            
            // Nếu không có trong cache, tìm trong database
            var contract = await _context.Contracts.FirstOrDefaultAsync(c => c.ProjectId == projectId && !c.Deleted);
            if (contract == null)
            {
                throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
            }
            
            // Lấy thông tin Project
            var projectList = await _dataService.ListProject(new SearchProjectDTO
            {
                ActionBy = actionBy,
                PageSize = int.MaxValue
            });
            
            var project = projectList.FirstOrDefault(p => p.Id == projectId);
            if (project == null)
            {
                throw new KeyNotFoundException(Message.SiteSurveyMessage.PROJECT_NOT_FOUND);
            }
            
            // Si es Executive Board, permitir acceso sin restricciones
            if (!isExecutiveBoard)
            {
                // Kiểm tra quyền truy cập - Allow all users associated with the project
                var hasAccess = await _context.ProjectUsers
                    .AnyAsync(pu => pu.ProjectId == projectId && pu.UserId == actionBy && !pu.Deleted);
                    
                if (!hasAccess)
                {
                    throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
                }
            }
            
            // Lấy danh sách ContractDetail
            var contractDetails = await _context.ContractDetails
                .Where(cd => cd.ContractId == contract.Id && !cd.Deleted)
                .Select(cd => new ContractDetailDTO
                {
                    WorkCode = cd.WorkCode,
                    Index = cd.Index,
                    ContractId = cd.ContractId,
                    ParentIndex = cd.ParentIndex,
                    WorkName = cd.WorkName,
                    Unit = cd.Unit,
                    Quantity = cd.Quantity,
                    UnitPrice = cd.UnitPrice,
                    Total = cd.Total,
                    CreatedAt = cd.CreatedAt,
                    Creator = cd.Creator,
                    UpdatedAt = cd.UpdatedAt,
                    Updater = cd.Updater,
                    Deleted = cd.Deleted
                })
                .ToListAsync();
                
            // Tạo ContractDTO
            var contractDTO = new ContractDTO
            {
                Id = contract.Id,
                ContractCode = contract.ContractCode,
                ContractName = contract.ContractName,
                Project = project,
                StartDate = contract.StartDate,
                EndDate = contract.EndDate,
                EstimatedDays = contract.EstimatedDays,
                Status = contract.Status,
                Tax = contract.Tax,
                SignDate = contract.SignDate,
                Attachments = contract.Attachments != null ? 
                    System.Text.Json.JsonSerializer.Deserialize<List<AttachmentInfo>>(contract.Attachments.RootElement.ToString()) 
                    : null,
                UpdatedAt = contract.UpdatedAt,
                Updater = contract.Updater,
                CreatedAt = contract.CreatedAt,
                Creator = contract.Creator,
                Deleted = contract.Deleted,
                ContractDetails = contractDetails
            };
            
            // Cập nhật cache nếu cần
            if (userContracts != null)
            {
                userContracts.Add(contractDTO);
                _ = _cacheService.SetAsync(userContractCacheKey, userContracts, TimeSpan.FromMinutes(30));
            }
            else
            {
                userContracts = new List<ContractDTO> { contractDTO };
                _ = _cacheService.SetAsync(userContractCacheKey, userContracts, TimeSpan.FromMinutes(30));
            }

            return contractDTO;
        }

        public async Task<ContractDTO> Save(SaveContractDTO model)
        {
            // Kiểm tra vai trò người dùng
            if (!_helpService.IsInRole(model.ActionBy, new List<string> { RoleConstValue.BUSINESS_EMPLOYEE, RoleConstValue.EXECUTIVE_BOARD }))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Clear tracking context to start with a clean state
            _context.ChangeTracker.Clear();

            // Kiểm tra Project tồn tại
            var projectList = await _dataService.ListProject(new SearchProjectDTO
            {
                ActionBy = model.ActionBy,
                PageSize = int.MaxValue
            });
            
            var project = projectList.FirstOrDefault(t => t.Id == model.ProjectId);
            if(project == null)
            {
                throw new KeyNotFoundException(Message.SiteSurveyMessage.PROJECT_NOT_FOUND);
            }

            // Kiểm tra xem người dùng có phải là người tạo Project không
            var isProjectCreator = (project.Creator == model.ActionBy); 
                
            if (!isProjectCreator)
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Kiểm tra các ràng buộc khác
            if(model.StartDate > model.EndDate)
            {
                throw new ArgumentException(Message.ProjectMessage.INVALID_DATE);
            }
            
            // Check if project already has a contract (for new contract creation)
            if (model.Id == 0)
            {
                var existingContract = await _context.Contracts
                    .FirstOrDefaultAsync(c => c.ProjectId == model.ProjectId && !c.Deleted);
                
                if (existingContract != null)
                {
                    throw new InvalidOperationException(Message.ContractMessage.PROJECT_ALREADY_HAS_CONTRACT);
                }
            }

            // Handle file attachments
            List<AttachmentInfo> attachmentInfos = new List<AttachmentInfo>();
            string existingAttachmentsJson = null;

            if (model.Id != 0)
            {
                // Find the entity directly instead of loading all contracts first
                var existingContract = await _context.Contracts.FirstOrDefaultAsync(t => t.Id == model.Id && !t.Deleted);
                if (existingContract == null)
                {
                    throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
                }

                // Check for duplicate contract code excluding current contract
                if (await _context.Contracts.AnyAsync(t => t.ContractCode == model.ContractCode && t.Id != model.Id && !t.Deleted))
                {
                    throw new ArgumentException(Message.ContractMessage.CONTRACT_CODE_EXIST);
                }
                
                if (existingContract?.Attachments != null)
                {
                    existingAttachmentsJson = existingContract.Attachments.RootElement.ToString();
                    attachmentInfos = System.Text.Json.JsonSerializer.Deserialize<List<AttachmentInfo>>(existingAttachmentsJson);
                }
            }
            else
            {
                // Check for duplicate contract code for new contracts
                if (await _context.Contracts.AnyAsync(t => t.ContractCode == model.ContractCode && !t.Deleted))
                {
                    throw new ArgumentException(Message.ContractMessage.CONTRACT_CODE_EXIST);
                }
            }

            if (model.Attachments != null && model.Attachments.Any())
            {
                // If there are existing attachments and we're uploading new ones, delete the old ones
                if (attachmentInfos != null && attachmentInfos.Any())
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

                // Upload new files
                foreach (var file in model.Attachments)
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
                            WebContentLink = uploadResult
                        });
                    }
                }
            }

            // Sử dụng transaction để đảm bảo tính toàn vẹn, nếu có lỗi sẽ rollback toàn bộ thay đổi
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                Contract contract;
                
                if (model.Id != 0)
                {
                    // Update existing contract - fetch it directly and update its properties
                    contract = await _context.Contracts.FirstOrDefaultAsync(t => t.Id == model.Id && !t.Deleted);
                    
                    // Update properties on the tracked entity instead of creating a new one
                    contract.ProjectId = model.ProjectId;
                    contract.ContractCode = model.ContractCode;
                    contract.ContractName = model.ContractName;
                    contract.StartDate = model.StartDate;
                    contract.EndDate = model.EndDate;
                    contract.EstimatedDays = model.EstimatedDays;
                    contract.Status = model.Status;
                    contract.Tax = model.Tax;
                    contract.SignDate = model.SignDate;
                    contract.Attachments = attachmentInfos.Any() ? 
                        JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(attachmentInfos)) : null;
                    contract.UpdatedAt = DateTime.UtcNow;
                    contract.Updater = model.ActionBy;
                    
                    _context.Update(contract);
                }
                else
                {
                    // Create new contract
                    contract = new Contract()
                    {
                        ContractCode = model.ContractCode,
                        ContractName = model.ContractName,
                        ProjectId = model.ProjectId,
                        StartDate = model.StartDate,
                        EndDate = model.EndDate,
                        EstimatedDays = model.EstimatedDays,
                        Status = model.Status,
                        Tax = model.Tax,
                        SignDate = model.SignDate,
                        Attachments = attachmentInfos.Any() ? 
                            JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(attachmentInfos)) : null,
                        CreatedAt = DateTime.UtcNow,
                        Creator = model.ActionBy,
                        UpdatedAt = DateTime.UtcNow,
                        Updater = model.ActionBy,
                        Deleted = false
                    };

                    await _context.AddAsync(contract);
                    await _context.SaveChangesAsync(); // Save to get the ID
                }

                // Lấy tất cả các ContractDetail của Contract này
                var existingContractDetails = await _context.ContractDetails
                    .Where(cd => cd.ContractId == contract.Id && !cd.Deleted)
                    .ToListAsync();

                // Detach all existing contract details to avoid tracking conflicts
                foreach (var detail in existingContractDetails)
                {
                    _context.Entry(detail).State = EntityState.Detached;
                }

                // Tạo dictionary để theo dõi WorkCodes đã được xử lý
                var processedWorkCodes = new HashSet<string>();
                
                // Xử lý danh sách ContractDetail
                List<ContractDetail> updatedContractDetails = new List<ContractDetail>();
                
                // Xử lý các item bị xóa trước
                foreach (var detailDto in model.ContractDetails.Where(d => d.IsDelete && !string.IsNullOrEmpty(d.WorkCode)))
                {
                    var detailToDelete = existingContractDetails.FirstOrDefault(cd => cd.WorkCode == detailDto.WorkCode);
                    if (detailToDelete != null)
                    {
                        detailToDelete.Deleted = true;
                        detailToDelete.UpdatedAt = DateTime.UtcNow;
                        detailToDelete.Updater = model.ActionBy;
                        _context.Update(detailToDelete);
                        processedWorkCodes.Add(detailDto.WorkCode);
                    }
                }
                
                // Xử lý các chi tiết theo cấp bậc - sắp xếp theo index để đảm bảo thứ tự xử lý
                // Sắp xếp các ContractDetails theo thứ tự từ parent đến child
                var sortedDetails = model.ContractDetails
                    .Where(d => !d.IsDelete)
                    .OrderBy(d => d.Index.Length) // Sắp xếp theo độ dài của index (1 trước, sau đó 1.1, 1.1.1)
                    .ThenBy(d => d.Index) // Sau đó sắp xếp theo thứ tự của index
                    .ToList();
                    
                // Dictionary để lưu trữ ánh xạ giữa Index và WorkCode mới tạo ra
                var indexToWorkCode = new Dictionary<string, string>();
                int nextCodeCounter = 1;
                
                // Dictionary lưu trữ ánh xạ từ Index string đến số index trong database
                var parentIndices = new Dictionary<string, string>();

                // Xử lý theo nhóm cấp bậc (level)
                var detailsByLevel = sortedDetails
                    .GroupBy(d => d.Index.Count(c => c == '.'))
                    .OrderBy(g => g.Key)
                    .ToList();

                foreach (var levelGroup in detailsByLevel)
                {
                    foreach (var detailDto in levelGroup)
                    {
                        // Bỏ qua nếu đã được xử lý
                        if (processedWorkCodes.Contains(detailDto.WorkCode))
                        {
                            continue;
                        }
                        
                        // Kiểm tra xem WorkCode này đã có trong Context chưa để tránh duplicate tracking
                        if (!string.IsNullOrEmpty(detailDto.WorkCode))
                        {
                            var tracked = _context.ChangeTracker.Entries<ContractDetail>()
                                .FirstOrDefault(e => e.Entity.WorkCode == detailDto.WorkCode);
                                
                            if (tracked != null)
                            {
                                // Entity đã được track, đánh dấu là đã xử lý
                                processedWorkCodes.Add(detailDto.WorkCode);
                                continue;
                            }
                        }
                        
                        // Tạo entity từ DTO
                        var contractDetail = new ContractDetail
                        {
                            ContractId = contract.Id,
                            Index = detailDto.Index,
                            WorkName = detailDto.WorkName,
                            Unit = detailDto.Unit,
                            Quantity = detailDto.Quantity,
                            UnitPrice = detailDto.UnitPrice,
                            Total = detailDto.Quantity * detailDto.UnitPrice,
                            UpdatedAt = DateTime.UtcNow,
                            Updater = model.ActionBy,
                            Deleted = false
                        };
                        
                        // Xử lý ParentIndex
                        if (string.IsNullOrEmpty(detailDto.ParentIndex))
                        {
                            contractDetail.ParentIndex = null;
                        }
                        else if (parentIndices.ContainsKey(detailDto.ParentIndex))
                        {
                            // Sử dụng WorkCode của parent thay vì sử dụng Index string
                            contractDetail.ParentIndex = parentIndices[detailDto.ParentIndex];
                        }
                        
                        // Nếu WorkCode không được cung cấp, tạo mới
                        if (string.IsNullOrEmpty(detailDto.WorkCode))
                        {
                            // Tạo WorkCode theo công thức ContractCode-số thứ tự tăng dần
                            contractDetail.WorkCode = $"{model.ContractCode}-{nextCodeCounter++}";
                            contractDetail.CreatedAt = DateTime.UtcNow;
                            contractDetail.Creator = model.ActionBy;
                            
                            await _context.ContractDetails.AddAsync(contractDetail);
                            
                            // Lưu mapping của index và workcode để sử dụng cho các hạng mục con
                            indexToWorkCode[detailDto.Index] = contractDetail.WorkCode;
                            parentIndices[detailDto.Index] = contractDetail.WorkCode;
                        }
                        else
                        {
                            // Kiểm tra xem WorkCode có tồn tại không
                            var existingDetail = existingContractDetails.FirstOrDefault(cd => cd.WorkCode == detailDto.WorkCode);
                            if (existingDetail != null)
                            {
                                // Cập nhật
                                contractDetail.WorkCode = detailDto.WorkCode;
                                contractDetail.CreatedAt = existingDetail.CreatedAt;
                                contractDetail.Creator = existingDetail.Creator;

                                // Sử dụng Entry để cập nhật trạng thái thay vì Update trực tiếp
                                _context.Entry(contractDetail).State = EntityState.Modified;
                                
                                // Cập nhật mapping
                                indexToWorkCode[detailDto.Index] = contractDetail.WorkCode;
                                parentIndices[detailDto.Index] = contractDetail.WorkCode;
                            }
                            else
                            {
                                // WorkCode không tồn tại, tạo mới với WorkCode đã cung cấp
                                contractDetail.WorkCode = detailDto.WorkCode;
                                contractDetail.CreatedAt = DateTime.UtcNow;
                                contractDetail.Creator = model.ActionBy;
                                
                                // Kiểm tra xem entity có đang được track hay không
                                var trackedEntry = _context.ChangeTracker.Entries<ContractDetail>()
                                    .FirstOrDefault(e => e.Entity.WorkCode == contractDetail.WorkCode);
                                    
                                if (trackedEntry == null)
                                {
                                    await _context.ContractDetails.AddAsync(contractDetail);
                                }
                                
                                // Cập nhật mapping
                                indexToWorkCode[detailDto.Index] = contractDetail.WorkCode;
                                parentIndices[detailDto.Index] = contractDetail.WorkCode;
                            }
                        }

                        updatedContractDetails.Add(contractDetail);
                        processedWorkCodes.Add(contractDetail.WorkCode);
                    }
                    
                    // Lưu thay đổi sau mỗi cấp để đảm bảo parent đã tồn tại trước khi thêm child
                    try 
                    {
                        await _context.SaveChangesAsync();
                    }
                    catch (DbUpdateException ex)
                    {
                        // Try to recover from tracking errors
                        if (ex.InnerException != null && ex.InnerException.Message.Contains("tracked because another instance"))
                        {
                            Console.WriteLine("Detected tracking conflict, attempting to recover...");
                            
                            // Get all tracked entities of ContractDetail type
                            var trackedEntities = _context.ChangeTracker.Entries<ContractDetail>()
                                .Where(e => e.State != EntityState.Detached)
                                .Select(e => e.Entity)
                                .ToList();
                                
                            // Clear tracking
                            ClearEntityTracking(trackedEntities);
                            
                            // Try save again
                            await _context.SaveChangesAsync();
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
                
                // Commit transaction khi tất cả thay đổi đã thành công
                await transaction.CommitAsync();

                // Xóa cache liên quan
                _ = _cacheService.DeleteAsync(RedisCacheKey.CONTRACT_CACHE_KEY);
                _ = _cacheService.DeleteAsync(RedisCacheKey.CONTRACT_DETAIL_CACHE_KEY);
                
                // Xóa cache theo project
                string projectCacheKey = string.Format(RedisCacheKey.CONTRACT_BY_PROJECT_CACHE_KEY, model.ProjectId);
                _ = _cacheService.DeleteAsync(projectCacheKey);
                
                // Xóa cache của người dùng liên quan đến project
                var projectUsers = await _context.ProjectUsers
                    .Where(pu => pu.ProjectId == model.ProjectId && !pu.Deleted)
                    .ToListAsync();
                    
                foreach (var pu in projectUsers)
                {
                    string userContractCacheKey = string.Format(RedisCacheKey.CONTRACT_BY_USER_CACHE_KEY, pu.UserId);
                    string userContractDetailCacheKey = string.Format(RedisCacheKey.CONTRACT_DETAIL_BY_USER_CACHE_KEY, pu.UserId);
                    _ = _cacheService.DeleteAsync(userContractCacheKey);
                    _ = _cacheService.DeleteAsync(userContractDetailCacheKey);
                }

                // Return the Contract DTO
                return await Detail(model.ProjectId, model.ActionBy);
            }
            catch (DbUpdateException ex)
            {
                // Xử lý lỗi cập nhật database
                await transaction.RollbackAsync();
                
                // Kiểm tra nếu lỗi là do tracking conflict
                if (ex.InnerException != null && ex.InnerException.Message.Contains("tracked because another instance"))
                {
                    throw new InvalidOperationException("Có xung đột trong quá trình cập nhật dữ liệu. Vui lòng thử lại sau.", ex);
                }
                
                throw;
            }
            catch (Exception ex)
            {
                // Có lỗi xảy ra, rollback tất cả thay đổi
                await transaction.RollbackAsync();
                Console.WriteLine($"Save contract error: {ex.Message}");
                throw; // Re-throw để xử lý exception ở tầng cao hơn
            }
        }
    }
}
