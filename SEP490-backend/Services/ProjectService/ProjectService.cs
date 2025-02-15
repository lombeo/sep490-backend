using Microsoft.EntityFrameworkCore;
using NPOI.HSSF.Record.Chart;
using NPOI.SS.Formula.Functions;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.ProjectDTO;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Services.CacheService;
using Sep490_Backend.Services.HelperService;

namespace Sep490_Backend.Services.ProjectService
{
    public interface IProjectService
    {
        Task<List<ProjectDTO>> List(SearchProjectDTO model);
        Task<ProjectDTO> Save(Project model, int actionBy);
        Task<int> Delete(int id, int actionBy);
        Task<ListProjectStatusDTO> ListProjectStatus(int actionBy);
        Task<ProjectDTO> Detail(int id, int actionBy);
    }

    public class ProjectService : IProjectService
    {
        private readonly BackendContext _context;
        private readonly ICacheService _cacheService;
        private readonly IHelperService _helperService;

        public ProjectService(BackendContext context, ICacheService cacheService, IHelperService helperService)
        {
            _context = context;
            _cacheService = cacheService;
            _helperService = helperService;
        }

        public async Task<int> Delete(int id, int actionBy)
        {
            if (!_helperService.IsInRole(actionBy, RoleConstValue.BUSINESS_EMPLOYEE))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            var data = await _context.Projects.FirstOrDefaultAsync(t => t.Id == id);
            if (data == null)
            {
                throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
            }
            data.Deleted = true;
            _context.Update(data);
            await _context.SaveChangesAsync();

            _ = _cacheService.DeleteAsync(RedisCacheKey.PROJECT_CACHE_KEY);

            return data.Id;
        }

        public async Task<ProjectDTO> Detail(int id, int actionBy)
        {
            if (!_helperService.IsInRole(actionBy, new List<string> { RoleConstValue.BUSINESS_EMPLOYEE, RoleConstValue.EXECUTIVE_BOARD }))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            var data = await List(new SearchProjectDTO()
            {
                ActionBy = actionBy
            });
            var result = data.FirstOrDefault(t => t.Id == id);
            if(result == null)
            {
                throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
            }
            return result;
        }

        public async Task<List<ProjectDTO>> List(SearchProjectDTO model)
        {
            if (!_helperService.IsInRole(model.ActionBy, RoleConstValue.BUSINESS_EMPLOYEE))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            string cacheKey = RedisCacheKey.PROJECT_CACHE_KEY;
            var data = await _cacheService.GetAsync<List<ProjectDTO>>(cacheKey);
            if (data == null)
            {
                data = _context.Projects.Where(t => !t.Deleted).Select(t => new ProjectDTO
                {
                    Id = t.Id,
                    ProjectCode = t.ProjectCode,
                    ProjectName = t.ProjectName,
                    Customer = _context.Customers.FirstOrDefault(c => c.Id == t.CustomerId) ?? new Customer(),
                    ConstructType = t.ConstructType,
                    Location = t.Location,
                    Area = t.Area,
                    Purpose = t.Purpose,
                    TechnicalReqs = t.TechnicalReqs,
                    StartDate = t.StartDate,
                    EndDate = t.EndDate,
                    Budget = t.Budget,
                    Status = t.Status,
                    Attachment = t.Attachment,
                    Description = t.Description,
                    UpdatedAt = t.UpdatedAt,
                    Updater = t.Updater,
                    CreatedAt = t.CreatedAt,
                    Creator = t.Creator,
                    Deleted = t.Deleted
                }).ToList();

                _ = _cacheService.SetAsync(cacheKey, data);
            }
            if (!string.IsNullOrWhiteSpace(model.KeyWord))
            {
                data = data.Where(t => t.ProjectCode.ToLower().Trim().Contains(model.KeyWord.ToLower().Trim())
                                        || t.ProjectName.ToLower().Trim().Contains(model.KeyWord.ToLower().Trim())
                                        || t.Customer.CustomerCode.ToLower().Trim().Contains(model.KeyWord.ToLower().Trim())
                                        || (t.Location ?? "").ToLower().Trim().Contains(model.KeyWord.ToLower().Trim())
                                        || t.Customer.CustomerName.ToLower().Trim().Contains(model.KeyWord.ToLower().Trim())).ToList();
            }
            if (model.CustomerId != 0)
            {
                data = data.Where(t => t.Customer.Id == model.CustomerId).ToList();
            }
            if (model.Status != null)
            {
                data = data.Where(t => t.Status == model.Status).ToList();
            }

            model.Total = data.Count();

            if (model.PageSize > 0)
            {
                data = data.Skip(model.Skip).Take(model.PageSize).ToList();
            }

            return data;
        }

        public async Task<ListProjectStatusDTO> ListProjectStatus(int actionBy)
        {
            if (!_helperService.IsInRole(actionBy, new List<string> { RoleConstValue.BUSINESS_EMPLOYEE , RoleConstValue.EXECUTIVE_BOARD}))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            var data = await List(new SearchProjectDTO()
            {
                ActionBy = actionBy
            });
            var result = new ListProjectStatusDTO
            {
                ReceiveRequest = data.Where(t => t.Status == Infra.Enums.ProjectStatusEnum.ReceiveRequest).Count(),
                Planning = data.Where(t => t.Status == Infra.Enums.ProjectStatusEnum.Planning).Count(),
                InProgress = data.Where(t => t.Status == Infra.Enums.ProjectStatusEnum.InProgress).Count(),
                Completed = data.Where(t => t.Status == Infra.Enums.ProjectStatusEnum.Completed).Count(),
                Paused = data.Where(t => t.Status == Infra.Enums.ProjectStatusEnum.Paused).Count(),
                Closed = data.Where(t => t.Status == Infra.Enums.ProjectStatusEnum.Closed).Count(),
                Total = data.Count()
            };

            return result;
        }

        public async Task<ProjectDTO> Save(Project model, int actionBy)
        {
            if (!_helperService.IsInRole(actionBy, RoleConstValue.BUSINESS_EMPLOYEE))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            var customer = _context.Customers.Where(t => !t.Deleted).ToList();
            var data = _context.Projects.Where(t => !t.Deleted).ToList();
            if(model.StartDate < model.EndDate)
            {
                throw new ArgumentException(Message.ProjectMessage.INVALID_DATE);
            }
            if(customer.FirstOrDefault(t => t.Id == model.CustomerId) == null)
            {
                throw new KeyNotFoundException(Message.CustomerMessage.CUSTOMER_NOT_FOUND);
            }
            var project = new Project()
            {
                ProjectName = model.ProjectName,
                CustomerId = model.CustomerId,
                ConstructType = model.ConstructType,
                Location = model.Location,
                Area = model.Area,
                Purpose = model.Purpose,
                TechnicalReqs = model.TechnicalReqs,
                StartDate = model.StartDate,
                EndDate = model.EndDate,
                Budget = model.Budget,
                Status = model.Status,
                Attachment = model.Attachment,
                Description = model.Description,
                UpdatedAt = DateTime.UtcNow,
                Updater = actionBy
            };

            if(model.Id != 0)
            {
                var entity = data.FirstOrDefault(t => t.Id == model.Id);
                if (entity == null)
                {
                    throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
                }
                if(data.FirstOrDefault(t => t.ProjectCode == model.ProjectCode && t.ProjectCode != entity.ProjectCode) != null)
                {
                    throw new ArgumentException(Message.ProjectMessage.PROJECT_CODE_EXIST);
                }
                project.ProjectCode = model.ProjectCode;
                project.CreatedAt = entity.CreatedAt;
                project.Creator = entity.Creator;

                entity = project;
                _context.Update(entity);
            }
            else
            {
                if (data.FirstOrDefault(t => t.ProjectCode == model.ProjectCode) != null)
                {
                    throw new ArgumentException(Message.ProjectMessage.PROJECT_CODE_EXIST);
                }

                project.ProjectCode = model.ProjectCode;
                project.CreatedAt = DateTime.UtcNow;
                project.Creator = actionBy;

                await _context.AddAsync(project);
            }

            await _context.SaveChangesAsync();
            _ = _cacheService.DeleteAsync(RedisCacheKey.PROJECT_CACHE_KEY);

            return new ProjectDTO
            {
                Id = project.Id,
                ProjectCode = project.ProjectCode,
                ProjectName = project.ProjectName,
                Customer = customer.FirstOrDefault(c => c.Id == project.CustomerId) ?? new Customer(),
                ConstructType = project.ConstructType,
                Location = project.Location,
                Area = project.Area,
                Purpose = project.Purpose,
                TechnicalReqs = project.TechnicalReqs,
                StartDate = project.StartDate,
                EndDate = project.EndDate,
                Budget = project.Budget,
                Status = project.Status,
                Attachment = project.Attachment,
                Description = project.Description,
                UpdatedAt = project.UpdatedAt,
                Updater = project.Updater,
                CreatedAt = project.CreatedAt,
                Creator = project.Creator,
                Deleted = project.Deleted
            };
        }
    }
}
