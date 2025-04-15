using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NPOI.SS.Formula.Functions;
using Sep490_Backend.DTO;
using Sep490_Backend.DTO.Contract;
using Sep490_Backend.DTO.Project;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Infra.Helps;
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
            var contract = await _context.Contracts
                .Include(c => c.ContractDetails)
                .FirstOrDefaultAsync(t => t.ProjectId == projectId && !t.Deleted);
                
            if (contract == null)
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
            
            // Use a transaction to ensure atomic operations
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                // Hard delete all ContractDetails associated with this contract
                var contractDetails = await _context.ContractDetails
                    .Where(cd => cd.ContractId == contract.Id)
                    .ToListAsync();
                    
                if (contractDetails.Any())
                {
                    _context.ContractDetails.RemoveRange(contractDetails);
                    await _context.SaveChangesAsync();
                }
                
                // Soft delete the main contract
                contract.Deleted = true;
                contract.UpdatedAt = DateTime.UtcNow;
                contract.Updater = actionBy;
                await _context.SaveChangesAsync();
                
                await transaction.CommitAsync();
                
                // Xóa toàn bộ cache liên quan
                await InvalidateContractCaches(contract.Id, projectId);
                
                return contract.Id;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Xóa toàn bộ cache liên quan đến contract
        /// </summary>
        private async Task InvalidateContractCaches(int contractId, int projectId)
        {
            // Danh sách các cache key chính cần xóa
            var cacheKeysToDelete = new List<string>
            {
                RedisCacheKey.CONTRACT_CACHE_KEY,
                RedisCacheKey.CONTRACT_DETAIL_CACHE_KEY,
                RedisCacheKey.PROJECT_CACHE_KEY,
                RedisCacheKey.CONTRACT_LIST_CACHE_KEY,
                string.Format(RedisCacheKey.CONTRACT_BY_ID_CACHE_KEY, contractId),
                string.Format(RedisCacheKey.CONTRACT_BY_PROJECT_CACHE_KEY, projectId)
            };
            
            // Xóa từng cache key
            foreach (var key in cacheKeysToDelete)
            {
                await _cacheService.DeleteAsync(key);
            }
            
            // Xóa các cache theo pattern
            await _cacheService.DeleteByPatternAsync(RedisCacheKey.CONTRACT_ALL_PATTERN);
        }

        public async Task<ContractDTO> Detail(int projectId, int actionBy)
        {
            // Tạo cache key cho contract theo project
            string cacheKey = string.Format(RedisCacheKey.CONTRACT_BY_PROJECT_CACHE_KEY, projectId);
            
            // Thử lấy dữ liệu từ cache trước
            var contractDTO = await _cacheService.GetAsync<ContractDTO>(cacheKey);
            
            if (contractDTO != null)
            {
                // Kiểm tra quyền truy cập với dữ liệu từ cache
                var cachedUser = StaticVariable.UserMemory.FirstOrDefault(u => u.Id == actionBy);
                bool isCachedUserExecutiveBoard = cachedUser != null && cachedUser.Role == RoleConstValue.EXECUTIVE_BOARD;
                
                if (!isCachedUserExecutiveBoard)
                {
                    var hasAccess = await _context.ProjectUsers
                        .AnyAsync(pu => pu.ProjectId == projectId && pu.UserId == actionBy && !pu.Deleted);
                        
                    if (!hasAccess)
                    {
                        throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
                    }
                }
                
                return contractDTO;
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
            
            // Xác thực người dùng
            var user = StaticVariable.UserMemory.FirstOrDefault(u => u.Id == actionBy);
            bool isExecutiveBoard = user != null && user.Role == RoleConstValue.EXECUTIVE_BOARD;
            
            // Kiểm tra quyền truy cập
            if (!isExecutiveBoard)
            {
                var hasAccess = await _context.ProjectUsers
                    .AnyAsync(pu => pu.ProjectId == projectId && pu.UserId == actionBy && !pu.Deleted);
                    
                if (!hasAccess)
                {
                    throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
                }
            }
            
            // Lấy danh sách ContractDetail - Using hard deletion, so no need to check Deleted flag
            var contractDetails = await _context.ContractDetails
                .Where(cd => cd.ContractId == contract.Id)
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
            
            // Map dữ liệu từ database thành DTO
            contractDTO = new ContractDTO
            {
                Id = contract.Id,
                ContractCode = contract.ContractCode,
                ContractName = contract.ContractName,
                ProjectId = contract.ProjectId,
                Project = project,
                StartDate = contract.StartDate,
                EndDate = contract.EndDate,
                EstimatedDays = contract.EstimatedDays,
                Status = contract.Status,
                Tax = contract.Tax,
                SignDate = contract.SignDate,
                Attachments = contract.Attachments != null ? 
                    JsonSerializer.Deserialize<List<AttachmentInfo>>(contract.Attachments.RootElement.ToString()) 
                    : null,
                CreatedAt = contract.CreatedAt,
                Creator = contract.Creator,
                UpdatedAt = contract.UpdatedAt,
                Updater = contract.Updater,
                ContractDetails = contractDetails
            };
            
            // Lưu vào cache
            await _cacheService.SetAsync(cacheKey, contractDTO, TimeSpan.FromHours(1));
            
            // Cập nhật cache chính của tất cả contract
            await UpdateContractCache(contractDTO);
            
            return contractDTO;
        }
        
        // Helper method để cập nhật cache chính của tất cả contract
        private async Task UpdateContractCache(ContractDTO newContractDTO)
        {
            var allContracts = await _cacheService.GetAsync<List<ContractDTO>>(RedisCacheKey.CONTRACT_CACHE_KEY);
            
            if (allContracts == null)
            {
                // Nếu cache trống, lấy toàn bộ dữ liệu và cập nhật cache
                allContracts = await GetAllContractDTOs();
                await _cacheService.SetAsync(RedisCacheKey.CONTRACT_CACHE_KEY, allContracts, TimeSpan.FromHours(1));
            }
            else 
            {
                // Tìm và cập nhật hoặc thêm mới contract trong cache
                var existingContract = allContracts.FirstOrDefault(c => c.Id == newContractDTO.Id);
                if (existingContract != null)
                {
                    // Cập nhật contract đã tồn tại
                    var index = allContracts.IndexOf(existingContract);
                    allContracts[index] = newContractDTO;
                }
                else
                {
                    // Thêm mới contract vào cache
                    allContracts.Add(newContractDTO);
                }
                
                await _cacheService.SetAsync(RedisCacheKey.CONTRACT_CACHE_KEY, allContracts, TimeSpan.FromHours(1));
            }
            
            // Cập nhật cache danh sách contract
            await _cacheService.SetAsync(RedisCacheKey.CONTRACT_LIST_CACHE_KEY, allContracts, TimeSpan.FromHours(1));
        }
        
        // Helper method to get all contracts as DTOs
        private async Task<List<ContractDTO>> GetAllContractDTOs()
        {
            var contracts = await _context.Contracts
                .Where(c => !c.Deleted)
                .ToListAsync();
                
            List<ContractDTO> contractDTOs = new List<ContractDTO>();
            
            foreach (var contract in contracts)
            {
                var project = await _context.Projects
                    .Include(p => p.ProjectUsers)
                    .FirstOrDefaultAsync(p => p.Id == contract.ProjectId && !p.Deleted);
                    
                if (project != null)
                {
                    // Get full project details
                    var customer = await _context.Customers
                        .FirstOrDefaultAsync(c => c.Id == project.CustomerId && !c.Deleted);
                    
                    // Using hard deletion for contract details, so no need to check Deleted flag
                    var contractDetails = await _context.ContractDetails
                        .Where(cd => cd.ContractId == contract.Id)
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
                        
                    // Map to ProjectDTO with all properties
                    var projectDTO = new ProjectDTO
                    {
                        Id = project.Id,
                        ProjectName = project.ProjectName,
                        ProjectCode = project.ProjectCode,
                        ConstructType = project.ConstructType,
                        Location = project.Location,
                        Area = project.Area,
                        Purpose = project.Purpose,
                        TechnicalReqs = project.TechnicalReqs,
                        StartDate = project.StartDate,
                        EndDate = project.EndDate,
                        Budget = project.Budget,
                        Status = project.Status,
                        Description = project.Description,
                        Customer = customer != null ? new Customer {
                            Id = customer.Id,
                            CustomerName = customer.CustomerName,
                            CustomerCode = customer.CustomerCode,
                            DirectorName = customer.DirectorName,
                            Phone = customer.Phone,
                            Email = customer.Email,
                            Address = customer.Address,
                            TaxCode = customer.TaxCode,
                            Fax = customer.Fax,
                            BankAccount = customer.BankAccount,
                            BankName = customer.BankName,
                            Description = customer.Description
                        } : new Customer(),
                        Attachments = project.Attachments != null ? 
                            JsonSerializer.Deserialize<List<AttachmentInfo>>(project.Attachments.RootElement.ToString()) 
                            : null,
                        CreatedAt = project.CreatedAt,
                        Creator = project.Creator,
                        UpdatedAt = project.UpdatedAt,
                        Updater = project.Updater,
                        ProjectUsers = project.ProjectUsers
                            .Where(pu => !pu.Deleted)
                            .Select(pu => new DTO.Project.ProjectUserDTO
                            {
                                Id = pu.Id,
                                UserId = pu.UserId,
                                ProjectId = pu.ProjectId,
                                IsCreator = pu.IsCreator
                            }).ToList()
                    };
                    
                    var dto = new ContractDTO
                    {
                        Id = contract.Id,
                        ContractCode = contract.ContractCode,
                        ContractName = contract.ContractName,
                        ProjectId = contract.ProjectId,
                        Project = projectDTO,
                        StartDate = contract.StartDate,
                        EndDate = contract.EndDate,
                        EstimatedDays = contract.EstimatedDays,
                        Status = contract.Status,
                        Tax = contract.Tax,
                        SignDate = contract.SignDate,
                        Attachments = contract.Attachments != null ? 
                            JsonSerializer.Deserialize<List<AttachmentInfo>>(contract.Attachments.RootElement.ToString()) 
                            : null,
                        CreatedAt = contract.CreatedAt,
                        Creator = contract.Creator,
                        UpdatedAt = contract.UpdatedAt,
                        Updater = contract.Updater,
                        ContractDetails = contractDetails
                    };
                    
                    contractDTOs.Add(dto);
                }
            }
            
            return contractDTOs;
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
                bool hasExistingContract = await _context.Contracts
                    .AsNoTracking()
                    .AnyAsync(c => c.ProjectId == model.ProjectId && !c.Deleted);
                
                if (hasExistingContract)
                {
                    throw new InvalidOperationException(Message.ContractMessage.PROJECT_ALREADY_HAS_CONTRACT);
                }
            }
            else
            {
                // Check contract exists
                bool contractExists = await _context.Contracts
                    .AsNoTracking()
                    .AnyAsync(t => t.Id == model.Id && !t.Deleted);
                    
                if (!contractExists)
                {
                    throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
                }

                // Check for duplicate contract code excluding current contract
                bool isDuplicateCode = await _context.Contracts
                    .AsNoTracking()
                    .AnyAsync(t => t.ContractCode == model.ContractCode && t.Id != model.Id && !t.Deleted);
                    
                if (isDuplicateCode)
                {
                    throw new ArgumentException(Message.ContractMessage.CONTRACT_CODE_EXIST);
                }
            }

            // Handle file attachments
            List<AttachmentInfo> attachmentInfos = new List<AttachmentInfo>();

            // If updating, get existing attachments
            if (model.Id != 0)
            {
                var existingAttachmentsJson = await _context.Contracts
                    .AsNoTracking()
                    .Where(t => t.Id == model.Id)
                    .Select(t => t.Attachments)
                    .FirstOrDefaultAsync();
                    
                if (existingAttachmentsJson != null)
                {
                    attachmentInfos = JsonSerializer.Deserialize<List<AttachmentInfo>>(
                        existingAttachmentsJson.RootElement.ToString()
                    ) ?? new List<AttachmentInfo>();
                }
            }

            // Process new attachments if any
            if (model.Attachments != null && model.Attachments.Any())
            {
                // Delete old attachments if necessary
                if (attachmentInfos.Any())
                {
                    try
                    {
                        var linksToDelete = attachmentInfos.Select(a => a.WebContentLink).ToList();
                        await _googleDriveService.DeleteFilesByLinks(linksToDelete);
                        attachmentInfos.Clear();
                    }
                    catch (Exception ex)
                    {
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

            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                // Store the contract ID for later use
                int contractId;
                
                // Process contract create/update
                if (model.Id == 0)
                {
                    // Create new contract
                    var newContract = new Contract
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
                        Attachments = attachmentInfos.Any() 
                            ? JsonDocument.Parse(JsonSerializer.Serialize(attachmentInfos)) 
                            : null,
                        CreatedAt = DateTime.UtcNow,
                        Creator = model.ActionBy,
                        UpdatedAt = DateTime.UtcNow,
                        Updater = model.ActionBy,
                        Deleted = false
                    };
                    
                    _context.Contracts.Add(newContract);
                    await _context.SaveChangesAsync();
                    
                    contractId = newContract.Id;
                    
                    // Clear context to avoid tracking conflicts
                    _context.ChangeTracker.Clear();
                }
                else
                {
                    // Get existing contract without tracking
                    contractId = model.Id;
                    
                    var existingContract = await _context.Contracts
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.Id == contractId);
                        
                    if (existingContract == null)
                    {
                        throw new KeyNotFoundException("Contract not found");
                    }
                    
                    // Create new contract instance for update
                    var updatedContract = new Contract
                    {
                        Id = contractId,
                        ContractCode = model.ContractCode,
                        ContractName = model.ContractName,
                        ProjectId = model.ProjectId,
                        StartDate = model.StartDate,
                        EndDate = model.EndDate,
                        EstimatedDays = model.EstimatedDays,
                        Status = model.Status,
                        Tax = model.Tax,
                        SignDate = model.SignDate,
                        Attachments = attachmentInfos.Any() 
                            ? JsonDocument.Parse(JsonSerializer.Serialize(attachmentInfos)) 
                            : existingContract.Attachments,
                        CreatedAt = existingContract.CreatedAt,
                        Creator = existingContract.Creator,
                        UpdatedAt = DateTime.UtcNow,
                        Updater = model.ActionBy,
                        Deleted = existingContract.Deleted
                    };
                    
                    // Mark entity as modified to trigger update
                    _context.Entry(updatedContract).State = EntityState.Modified;
                    await _context.SaveChangesAsync();
                    
                    // Clear context to avoid tracking conflicts
                    _context.ChangeTracker.Clear();
                }
                
                // Process contract details
                
                // 1. Get list of deleted items
                var itemsToDelete = model.ContractDetails
                    .Where(d => d.IsDelete && !string.IsNullOrEmpty(d.WorkCode))
                    .Select(d => d.WorkCode)
                    .ToList();
                    
                // 2. Get existing contract details without tracking
                var existingDetails = await _context.ContractDetails
                    .AsNoTracking()
                    .Where(cd => cd.ContractId == contractId)
                    .ToListAsync();
                    
                var existingWorkCodes = existingDetails.Select(cd => cd.WorkCode).ToHashSet();
                
                // 3. Delete items marked for deletion
                if (itemsToDelete.Any())
                {
                    foreach (var workCode in itemsToDelete)
                    {
                        var detailToDelete = existingDetails.FirstOrDefault(d => d.WorkCode == workCode);
                        if (detailToDelete != null)
                        {
                            // We need to attach and mark as deleted - cannot directly use Remove with a detached entity
                            _context.ContractDetails.Attach(detailToDelete);
                            _context.ContractDetails.Remove(detailToDelete);
                        }
                    }
                    
                    await _context.SaveChangesAsync();
                    
                    // Update the existing work codes list
                    existingWorkCodes.RemoveWhere(wc => itemsToDelete.Contains(wc));
                    
                    // Clear context again
                    _context.ChangeTracker.Clear();
                }
                
                // 4. Calculate next counter for new items
                int nextCounter = 1;
                if (existingWorkCodes.Any())
                {
                    var maxCounter = existingWorkCodes
                        .Where(wc => wc.StartsWith(model.ContractCode + "-"))
                        .Select(wc => {
                            var parts = wc.Split('-');
                            if (parts.Length > 1 && int.TryParse(parts[1], out int num))
                                return num;
                            return 0;
                        })
                        .DefaultIfEmpty(0)
                        .Max();
                    
                    nextCounter = maxCounter + 1;
                }
                
                // 5. Process other items by level to maintain parent-child relationships
                var indexToWorkCode = new Dictionary<string, string>();
                
                // Get items to process (not marked for deletion)
                var itemsToProcess = model.ContractDetails
                    .Where(d => !d.IsDelete)
                    .OrderBy(d => d.Index.Length)
                    .ThenBy(d => d.Index)
                    .ToList();
                    
                // Process each level to maintain hierarchy
                foreach (var level in itemsToProcess.GroupBy(item => item.Index.Count(c => c == '.')))
                {
                    var detailsForLevel = new List<ContractDetail>();
                    
                    foreach (var item in level)
                    {
                        // Skip if already in deleted list
                        if (itemsToDelete.Contains(item.WorkCode))
                            continue;
                        
                        // Resolve parent
                        string parentWorkCode = null;
                        if (!string.IsNullOrEmpty(item.ParentIndex) && indexToWorkCode.ContainsKey(item.ParentIndex))
                        {
                            parentWorkCode = indexToWorkCode[item.ParentIndex];
                        }
                        
                        // Handle updates
                        if (!string.IsNullOrEmpty(item.WorkCode) && existingWorkCodes.Contains(item.WorkCode))
                        {
                            // Get the existing detail
                            var existingDetail = existingDetails.FirstOrDefault(d => d.WorkCode == item.WorkCode);
                            
                            if (existingDetail != null)
                            {
                                // Create updated entity
                                var updatedDetail = new ContractDetail
                                {
                                    WorkCode = item.WorkCode,
                                    ContractId = contractId,
                                    Index = item.Index,
                                    ParentIndex = parentWorkCode ?? existingDetail.ParentIndex,
                                    WorkName = item.WorkName,
                                    Unit = item.Unit,
                                    Quantity = item.Quantity,
                                    UnitPrice = item.UnitPrice,
                                    Total = item.Quantity * item.UnitPrice,
                                    CreatedAt = existingDetail.CreatedAt,
                                    Creator = existingDetail.Creator,
                                    UpdatedAt = DateTime.UtcNow,
                                    Updater = model.ActionBy,
                                    Deleted = false
                                };
                                
                                // Mark for update
                                _context.Entry(updatedDetail).State = EntityState.Modified;
                                
                                // Store for parent-child relationship
                                indexToWorkCode[item.Index] = item.WorkCode;
                            }
                        }
                        else
                        {
                            // This is a new item
                            string workCode;
                            if (string.IsNullOrEmpty(item.WorkCode))
                            {
                                // Generate a new unique work code
                                workCode = $"{model.ContractCode}-{nextCounter++}";
                            }
                            else
                            {
                                // Use the provided work code if it doesn't exist yet
                                workCode = item.WorkCode;
                            }
                            
                            // Create new detail
                            var newDetail = new ContractDetail
                            {
                                WorkCode = workCode,
                                ContractId = contractId,
                                Index = item.Index,
                                ParentIndex = parentWorkCode,
                                WorkName = item.WorkName,
                                Unit = item.Unit,
                                Quantity = item.Quantity,
                                UnitPrice = item.UnitPrice,
                                Total = item.Quantity * item.UnitPrice,
                                CreatedAt = DateTime.UtcNow,
                                Creator = model.ActionBy,
                                UpdatedAt = DateTime.UtcNow,
                                Updater = model.ActionBy,
                                Deleted = false
                            };
                            
                            // Add to list for this level
                            detailsForLevel.Add(newDetail);
                            
                            // Store for parent-child relationship
                            indexToWorkCode[item.Index] = workCode;
                            
                            // Add to existing work codes to prevent duplicates
                            existingWorkCodes.Add(workCode);
                        }
                    }
                    
                    // Add new details for this level if any
                    if (detailsForLevel.Any())
                    {
                        await _context.ContractDetails.AddRangeAsync(detailsForLevel);
                    }
                    
                    // Save changes for this level
                    await _context.SaveChangesAsync();
                    
                    // Clear context to avoid tracking issues with the next level
                    _context.ChangeTracker.Clear();
                }
                
                // Commit the transaction
                await transaction.CommitAsync();
                
                // Clear caches
                await InvalidateContractCaches(contractId, model.ProjectId);
                
                // Get the updated contract details
                _context.ChangeTracker.Clear();
                return await Detail(model.ProjectId, model.ActionBy);
            }
            catch (Exception ex)
            {
                // Log detailed error information
                Console.WriteLine($"Save Contract Error: {ex.Message}");
                Console.WriteLine($"InnerException: {ex.InnerException?.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                
                // Rollback transaction
                await transaction.RollbackAsync();
                
                // Wrap in a more user-friendly message
                throw new InvalidOperationException(
                    "Có lỗi khi lưu dữ liệu hợp đồng. Vui lòng thử lại sau hoặc liên hệ quản trị viên.", ex);
            }
        }
    }
}

