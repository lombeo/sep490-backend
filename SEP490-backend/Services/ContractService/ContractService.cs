using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NPOI.SS.Formula.Functions;
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
        Task<int> Delete(int id, int actionBy);
        Task<ContractDTO> Detail(int id, int actionBy);
    }

    public class ContractService : IContractService
    {
        private readonly BackendContext _context;
        private readonly ICacheService _cacheService;
        private readonly IDataService _dataService;
        private readonly IHelperService _helpService;
        private readonly IGoogleDriveService _googleDriveService;

        // Định nghĩa các key cache cho Contract
        private const string CONTRACT_BY_PROJECT_CACHE_KEY = "CONTRACT:PROJECT:{0}"; // Pattern: CONTRACT:PROJECT:projectId
        private const string CONTRACT_BY_USER_CACHE_KEY = "CONTRACT:USER:{0}"; // Pattern: CONTRACT:USER:userId
        private const string CONTRACT_DETAIL_BY_USER_CACHE_KEY = "CONTRACT_DETAIL:USER:{0}"; // Pattern: CONTRACT_DETAIL:USER:userId

        public ContractService(BackendContext context, ICacheService cacheService, IDataService dataService, IHelperService helpService, IGoogleDriveService googleDriveService)
        {
            _context = context;
            _cacheService = cacheService;
            _dataService = dataService;
            _helpService = helpService;
            _googleDriveService = googleDriveService;
        }

        public async Task<int> Delete(int id, int actionBy)
        {
            // Kiểm tra vai trò người dùng
            if (!_helpService.IsInRole(actionBy, new List<string> { RoleConstValue.BUSINESS_EMPLOYEE, RoleConstValue.EXECUTIVE_BOARD }))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            
            // Tìm Contract cần xóa
            var data = await _context.Contracts.FirstOrDefaultAsync(t => t.Id == id && !t.Deleted);
            if (data == null)
            {
                throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
            }

            // Kiểm tra xem người dùng có phải là người tạo Project chứa Contract này không
            var projectCreator = await _context.ProjectUsers
                .FirstOrDefaultAsync(pu => pu.ProjectId == data.ProjectId && pu.IsCreator && !pu.Deleted);
                
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
                .Where(t => !t.Deleted && t.ContractId == id)
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
            string projectCacheKey = string.Format(CONTRACT_BY_PROJECT_CACHE_KEY, data.ProjectId);
            _ = _cacheService.DeleteAsync(projectCacheKey);
            
            // Xóa cache của người dùng liên quan đến project
            var projectUsers = await _context.ProjectUsers
                .Where(pu => pu.ProjectId == data.ProjectId && !pu.Deleted)
                .ToListAsync();
                
            foreach (var pu in projectUsers)
            {
                string userContractCacheKey = string.Format(CONTRACT_BY_USER_CACHE_KEY, pu.UserId);
                string userContractDetailCacheKey = string.Format(CONTRACT_DETAIL_BY_USER_CACHE_KEY, pu.UserId);
                _ = _cacheService.DeleteAsync(userContractCacheKey);
                _ = _cacheService.DeleteAsync(userContractDetailCacheKey);
            }

            return data.Id;
        }

        public async Task<ContractDTO> Detail(int id, int actionBy)
        {
            // Kiểm tra vai trò người dùng
            if (!_helpService.IsInRole(actionBy, new List<string>
            {
                RoleConstValue.EXECUTIVE_BOARD, RoleConstValue.BUSINESS_EMPLOYEE
            }))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Kiểm tra trong cache của người dùng trước
            string userContractCacheKey = string.Format(CONTRACT_BY_USER_CACHE_KEY, actionBy);
            var userContracts = await _cacheService.GetAsync<List<ContractDTO>>(userContractCacheKey);
            
            if (userContracts != null)
            {
                // Tìm contract trong cache
                var contractFromCache = userContracts.FirstOrDefault(c => c.Id == id);
                if (contractFromCache != null)
                {
                    return contractFromCache;
                }
            }
            
            // Nếu không có trong cache, tìm trong database
            var contract = await _context.Contracts.FirstOrDefaultAsync(c => c.Id == id && !c.Deleted);
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
            
            var project = projectList.FirstOrDefault(p => p.Id == contract.ProjectId);
            if (project == null)
            {
                throw new KeyNotFoundException(Message.SiteSurveyMessage.PROJECT_NOT_FOUND);
            }
            
            // Kiểm tra quyền truy cập
            var hasAccess = await _context.ProjectUsers
                .AnyAsync(pu => pu.ProjectId == contract.ProjectId && pu.UserId == actionBy && !pu.Deleted);
                
            if (!hasAccess)
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            
            // Lấy danh sách ContractDetail
            var contractDetails = await _context.ContractDetails
                .Where(cd => cd.ContractId == id && !cd.Deleted)
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

            var data = await _context.Contracts.Where(t => !t.Deleted).ToListAsync();

            // Handle file attachments
            List<AttachmentInfo> attachmentInfos = new List<AttachmentInfo>();
            string existingAttachmentsJson = null;

            if (model.Id != 0)
            {
                // If this is an update, get the existing contract to check old attachments
                var existingContract = await _context.Contracts.FirstOrDefaultAsync(t => t.Id == model.Id && !t.Deleted);
                if (existingContract == null)
                {
                    throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
                }
                
                if (existingContract?.Attachments != null)
                {
                    existingAttachmentsJson = existingContract.Attachments.RootElement.ToString();
                    attachmentInfos = System.Text.Json.JsonSerializer.Deserialize<List<AttachmentInfo>>(existingAttachmentsJson);
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

            var contract = new Contract()
            {
                ProjectId = model.ProjectId,
                StartDate = model.StartDate,
                EndDate = model.EndDate,
                EstimatedDays = model.EstimatedDays,
                Status = model.Status,
                Tax = model.Tax,
                SignDate = model.SignDate,
                Attachments = attachmentInfos.Any() ? JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(attachmentInfos)) : null,
                UpdatedAt = DateTime.UtcNow,
                Updater = model.ActionBy,
                Deleted = false
            };

            if (model.Id != 0)
            {
                var entity = data.FirstOrDefault(t => t.Id == model.Id);
                if(entity == null)
                {
                    throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
                }
                if(data.FirstOrDefault(t => t.ContractCode == model.ContractCode && t.ContractCode != entity.ContractCode) != null)
                {
                    throw new ArgumentException(Message.ContractMessage.CONTRACT_CODE_EXIST);
                }

                contract.ContractCode = model.ContractCode;
                contract.CreatedAt = entity.CreatedAt;
                contract.Creator = entity.Creator;
                contract.Id = entity.Id;

                _context.Update(contract);
            }
            else
            {
                if(data.FirstOrDefault(t => t.ContractCode == model.ContractCode) != null)
                {
                    throw new ArgumentException(Message.ContractMessage.CONTRACT_CODE_EXIST);
                }

                contract.ContractCode = model.ContractCode;
                contract.CreatedAt = DateTime.UtcNow;
                contract.Creator = model.ActionBy;

                await _context.AddAsync(contract);
                await _context.SaveChangesAsync(); // Lưu contract trước để có Id
            }

            // Lấy tất cả các ContractDetail của Contract này
            var existingContractDetails = _context.Set<ContractDetail>()
                .Where(cd => cd.ContractId == contract.Id && !cd.Deleted)
                .ToList();

            // Xử lý danh sách ContractDetail
            List<ContractDetail> updatedContractDetails = new List<ContractDetail>();
            int counter = 1;

            foreach (var detailDto in model.ContractDetails)
            {
                // Nếu isDelete = true và WorkCode tồn tại, thực hiện xóa mềm
                if (detailDto.IsDelete && !string.IsNullOrEmpty(detailDto.WorkCode))
                {
                    var detailToDelete = existingContractDetails.FirstOrDefault(cd => cd.WorkCode == detailDto.WorkCode);
                    if (detailToDelete != null)
                    {
                        detailToDelete.Deleted = true;
                        detailToDelete.UpdatedAt = DateTime.UtcNow;
                        detailToDelete.Updater = model.ActionBy;
                        _context.Update(detailToDelete);
                    }
                    continue;
                }

                // Tạo entity từ DTO
                var contractDetail = new ContractDetail
                {
                    ContractId = contract.Id,
                    Index = detailDto.Index,
                    ParentIndex = detailDto.ParentIndex,
                    WorkName = detailDto.WorkName,
                    Unit = detailDto.Unit,
                    Quantity = detailDto.Quantity,
                    UnitPrice = detailDto.UnitPrice,
                    UpdatedAt = DateTime.UtcNow,
                    Updater = model.ActionBy,
                    Deleted = false
                };

                // Nếu WorkCode không được cung cấp, tạo mới
                if (string.IsNullOrEmpty(detailDto.WorkCode))
                {
                    // Tạo WorkCode theo công thức ContractCode-số thứ tự tăng dần
                    contractDetail.WorkCode = $"{model.ContractCode}-{counter++}";
                    contractDetail.CreatedAt = DateTime.UtcNow;
                    contractDetail.Creator = model.ActionBy;
                    
                    await _context.AddAsync(contractDetail);
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

                        _context.Update(contractDetail);
                    }
                    else
                    {
                        // WorkCode không tồn tại, tạo mới với WorkCode đã cung cấp
                        contractDetail.WorkCode = detailDto.WorkCode;
                        contractDetail.CreatedAt = DateTime.UtcNow;
                        contractDetail.Creator = model.ActionBy;
                        
                        await _context.AddAsync(contractDetail);
                    }
                }

                updatedContractDetails.Add(contractDetail);
            }

            await _context.SaveChangesAsync();

            // Xóa cache liên quan
            _ = _cacheService.DeleteAsync(RedisCacheKey.CONTRACT_CACHE_KEY);
            _ = _cacheService.DeleteAsync(RedisCacheKey.CONTRACT_DETAIL_CACHE_KEY);
            
            // Xóa cache theo project
            string projectCacheKey = string.Format(CONTRACT_BY_PROJECT_CACHE_KEY, model.ProjectId);
            _ = _cacheService.DeleteAsync(projectCacheKey);
            
            // Xóa cache của người dùng liên quan đến project
            var projectUsers = await _context.ProjectUsers
                .Where(pu => pu.ProjectId == model.ProjectId && !pu.Deleted)
                .ToListAsync();
                
            foreach (var pu in projectUsers)
            {
                string userContractCacheKey = string.Format(CONTRACT_BY_USER_CACHE_KEY, pu.UserId);
                string userContractDetailCacheKey = string.Format(CONTRACT_DETAIL_BY_USER_CACHE_KEY, pu.UserId);
                _ = _cacheService.DeleteAsync(userContractCacheKey);
                _ = _cacheService.DeleteAsync(userContractDetailCacheKey);
            }

            // Chuyển đổi các ContractDetail thành DTO
            var contractDetailDTOs = updatedContractDetails.Select(cd => new ContractDetailDTO
            {
                WorkCode = cd.WorkCode,
                Index = cd.Index,
                ContractId = cd.ContractId,
                ParentIndex = cd.ParentIndex,
                WorkName = cd.WorkName,
                Unit = cd.Unit,
                Quantity = cd.Quantity,
                UnitPrice = cd.UnitPrice,
                CreatedAt = cd.CreatedAt,
                Creator = cd.Creator,
                UpdatedAt = cd.UpdatedAt,
                Updater = cd.Updater,
                Deleted = cd.Deleted
            }).ToList();

            return new ContractDTO
            {
                Id = contract.Id,
                ContractCode = contract.ContractCode,
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
                ContractDetails = contractDetailDTOs
            };
        }
    }
}
