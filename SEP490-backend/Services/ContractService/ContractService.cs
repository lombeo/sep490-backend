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
using Sep490_Backend.Services.HelperService;

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

        public ContractService(BackendContext context, ICacheService cacheService, IDataService dataService, IHelperService helpService)
        {
            _context = context;
            _cacheService = cacheService;
            _dataService = dataService;
            _helpService = helpService;
        }

        public async Task<int> Delete(int id, int actionBy)
        {
            if (!_helpService.IsInRole(actionBy, RoleConstValue.BUSINESS_EMPLOYEE))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            var data = await _context.Contracts.FirstOrDefaultAsync(t => t.Id == id);

            if (data == null)
            {
                throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
            }
            data.Deleted = true;
            _context.Update(data);
            var contractDetail = _context.ContractDetails.Where(t => !t.Deleted).ToList();
            foreach (var item in contractDetail)
            {
                if (item.ContractId == data.Id)
                {
                    item.Deleted = true;
                    _context.Update(item);
                }
            }

            await _context.SaveChangesAsync();

            _ = _cacheService.DeleteAsync(new List<string>
            {
                RedisCacheKey.CONTRACT_CACHE_KEY,
                RedisCacheKey.CONTRACT_DETAIL_CACHE_KEY
            });

            return data.Id;
        }

        public async Task<ContractDTO> Detail(int id, int actionBy)
        {
            if (!_helpService.IsInRole(actionBy, new List<string>
            {
                RoleConstValue.EXECUTIVE_BOARD, RoleConstValue.BUSINESS_EMPLOYEE
            }))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            var data = await _dataService.ListContract(new SearchContractDTO()
            {
                ActionBy = actionBy
            });

            var result = data.FirstOrDefault(t => t.Id == id);

            if (result == null)
            {
                throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
            }

            return result;
        }

        public async Task<ContractDTO> Save(SaveContractDTO model)
        {
            if (!_helpService.IsInRole(model.ActionBy, RoleConstValue.BUSINESS_EMPLOYEE))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            var project = await _dataService.ListProject(new DTO.Project.SearchProjectDTO
            {
                ActionBy = model.ActionBy
            });
            var data = _context.Contracts.Where(t => !t.Deleted).ToList();

            var contract = new Contract()
            {
                ProjectId = model.ProjectId,
                StartDate = model.StartDate,
                EndDate = model.EndDate,
                EstimatedDays = model.EstimatedDays,
                Status = model.Status,
                Tax = model.Tax,
                SignDate = model.SignDate,
                Attachment = model.Attachment,
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

                entity = contract;
                _context.Update(entity);
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
            }

            await _context.SaveChangesAsync();

            _ = _cacheService.DeleteAsync(RedisCacheKey.CONTRACT_CACHE_KEY);

            return new ContractDTO
            {
                Id = contract.Id,
                ContractCode = contract.ContractCode,
                Project = project.FirstOrDefault(p => p.Id == contract.ProjectId) ?? new ProjectDTO(),
                StartDate = contract.StartDate,
                EndDate = contract.EndDate,
                EstimatedDays = contract.EstimatedDays,
                Status = contract.Status,
                Tax = contract.Tax,
                SignDate = contract.SignDate,
                Attachment = contract.Attachment,
                UpdatedAt = contract.UpdatedAt,
                Updater = contract.Updater,
                CreatedAt = contract.CreatedAt,
                Creator = contract.Creator,
                Deleted = contract.Deleted
            };
        }
    }
}
