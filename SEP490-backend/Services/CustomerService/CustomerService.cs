using Microsoft.EntityFrameworkCore;
using Sep490_Backend.Controllers;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.Customer;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Infra.Helps;
using Sep490_Backend.Services.AuthenService;
using Sep490_Backend.Services.CacheService;
using Sep490_Backend.Services.DataService;
using Sep490_Backend.Services.EmailService;
using Sep490_Backend.Services.HelperService;
using System.Text.RegularExpressions;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sep490_Backend.Services.CustomerService
{
    public interface ICustomerService
    {
        Task<Customer> GetDetailCustomer(int customerId, int actionBy);
        Task<bool> DeleteCustomer(int customerId, int actionBy);
        Task<Customer> CreateCustomer(CustomerCreateDTO model, int actionBy);
        Task<Customer> UpdateCustomer(Customer model, int actionBy);
    }

    public class CustomerService : ICustomerService
    {
        private readonly BackendContext _context;
        private readonly IAuthenService _authenService;
        private readonly IHelperService _helperService;
        private readonly ICacheService _cacheService;
        private readonly IDataService _dataService;


        public CustomerService(BackendContext context, IAuthenService authenService, IEmailService emailService, IHelperService helperService, ICacheService cacheService, IDataService dataService)
        {
            _context = context;
            _authenService = authenService;
            _helperService = helperService;
            _cacheService = cacheService;
            _dataService = dataService;
        }

        public async Task<Customer> GetDetailCustomer(int customerId, int actionBy)
        {
            if (!_helperService.IsInRole(actionBy, new List<string> { RoleConstValue.BUSINESS_EMPLOYEE, RoleConstValue.EXECUTIVE_BOARD }))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            var customer = await _context.Customers
                .Where(t => t.Id == customerId && !t.Deleted)
                .FirstOrDefaultAsync();
            return customer;
        }


        public async Task<bool> DeleteCustomer(int customerId, int actionBy)
        {
            if (!_helperService.IsInRole(actionBy, RoleConstValue.BUSINESS_EMPLOYEE))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == customerId && !c.Deleted);
            if (customer == null)
            {
                throw new KeyNotFoundException(Message.CustomerMessage.CUSTOMER_NOT_FOUND);
            }
            customer.Deleted = true;
            _context.Update(customer);

            await _context.SaveChangesAsync();
            _ = _cacheService.DeleteAsync(RedisCacheKey.CUSTOMER_CACHE_KEY);

            return true;
        }

        public async Task<Customer> CreateCustomer(CustomerCreateDTO model, int actionBy)
        {
            var errors = new List<ResponseError>();

            // Authorization check
            if (!_helperService.IsInRole(actionBy, new List<string> { RoleConstValue.BUSINESS_EMPLOYEE }))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            if (string.IsNullOrWhiteSpace(model.CustomerCode))
                errors.Add(new ResponseError
                {
                    Message = Message.CommonMessage.MISSING_PARAM,
                    Field = nameof(model.CustomerCode).ToCamelCase()
                });

            if (string.IsNullOrWhiteSpace(model.TaxCode))
                errors.Add(new ResponseError
                {
                    Message = Message.CommonMessage.MISSING_PARAM,
                    Field = nameof(model.TaxCode).ToCamelCase()
                });

            if (!string.IsNullOrWhiteSpace(model.Email) && !Regex.IsMatch(model.Email, PatternConst.EMAIL_PATTERN))
                errors.Add(new ResponseError
                {
                    Message = Message.AuthenMessage.INVALID_EMAIL,
                    Field = nameof(model.Email).ToCamelCase()
                });

            var data = await _dataService.ListCustomer(new CustomerSearchDTO() { ActionBy = actionBy, PageSize = int.MaxValue });
            if (data.FirstOrDefault(t => t.CustomerCode == model.CustomerCode) != null)
            {
                errors.Add(new ResponseError
                {
                    Message = Message.CustomerMessage.CUSTOMER_CODE_DUPLICATE,
                    Field = nameof(model.CustomerCode).ToCamelCase()
                });
            }
            if (data.FirstOrDefault(t => t.TaxCode == model.TaxCode) != null)
            {
                errors.Add(new ResponseError
                {
                    Message = Message.CustomerMessage.TAX_CODE_DUPLICATE,
                    Field = nameof(model.TaxCode).ToCamelCase()
                });
            }
            if (!string.IsNullOrWhiteSpace(model.Fax) && data.FirstOrDefault(t => t.Fax == model.Fax) != null)
            {
                errors.Add(new ResponseError
                {
                    Message = Message.CustomerMessage.FAX_CODE_DUPLICATE,
                    Field = nameof(model.Fax).ToCamelCase()
                });
            }
            if (!string.IsNullOrWhiteSpace(model.BankAccount) && data.FirstOrDefault(t => t.BankAccount == model.BankAccount) != null)
            {
                errors.Add(new ResponseError
                {
                    Message = Message.CustomerMessage.BANK_ACCOUNT_DUPLICATE,
                    Field = nameof(model.BankAccount).ToCamelCase()
                });
            }
            if (!string.IsNullOrWhiteSpace(model.Email) && data.FirstOrDefault(t => t.Email == model.Email) != null)
            {
                errors.Add(new ResponseError
                {
                    Message = Message.CustomerMessage.CUSTOMER_EMAIL_DUPLICATE,
                    Field = nameof(model.Email).ToCamelCase()
                });
            }

            // Throw aggregated errors
            if (errors.Count > 0)
                throw new ValidationException(errors);

            var customer = new Customer
            {
                CustomerCode = model.CustomerCode,
                CustomerName = model.CustomerName ?? "",
                Email = model.Email ?? "",
                Phone = model.Phone ?? "",
                TaxCode = model.TaxCode,
                Fax = model.Fax ?? "",
                Address = model.Address ?? "",
                DirectorName = model.DirectorName ?? "",
                Description = model.Description ?? "",
                BankAccount = model.BankAccount ?? "",
                BankName = model.BankName ?? "",
                Updater = actionBy,
                Creator = actionBy,
            };
            await _context.AddAsync(customer);
            await _context.SaveChangesAsync();
            _ = _cacheService.DeleteAsync(RedisCacheKey.CUSTOMER_CACHE_KEY);

            return customer;
        }

        public async Task<Customer> UpdateCustomer(Customer model, int actionBy)
        {
            var errors = new List<ResponseError>();
            if (!_helperService.IsInRole(actionBy, RoleConstValue.BUSINESS_EMPLOYEE))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            var existCustomer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == model.Id);
            if (existCustomer == null)
            {
                throw new KeyNotFoundException(Message.CustomerMessage.CUSTOMER_NOT_FOUND);
            }

            if (string.IsNullOrWhiteSpace(model.CustomerCode))
                errors.Add(new ResponseError
                {
                    Message = Message.CommonMessage.MISSING_PARAM,
                    Field = nameof(model.CustomerCode).ToCamelCase()
                });

            if (string.IsNullOrWhiteSpace(model.TaxCode))
                errors.Add(new ResponseError
                {
                    Message = Message.CommonMessage.MISSING_PARAM,
                    Field = nameof(model.TaxCode).ToCamelCase()
                });

            if (!string.IsNullOrWhiteSpace(model.Email) && !Regex.IsMatch(model.Email, PatternConst.EMAIL_PATTERN))
                errors.Add(new ResponseError
                {
                    Message = Message.AuthenMessage.INVALID_EMAIL,
                    Field = nameof(model.Email).ToCamelCase()
                });

            var data = await _dataService.ListCustomer(new CustomerSearchDTO() { ActionBy = actionBy, PageSize = int.MaxValue });
            if (data.FirstOrDefault(t => t.CustomerCode == model.CustomerCode) != null)
            {
                errors.Add(new ResponseError
                {
                    Message = Message.CustomerMessage.CUSTOMER_CODE_DUPLICATE,
                    Field = nameof(model.CustomerCode).ToCamelCase()
                });
            }
            if (data.FirstOrDefault(t => t.TaxCode == model.TaxCode) != null)
            {
                errors.Add(new ResponseError
                {
                    Message = Message.CustomerMessage.TAX_CODE_DUPLICATE,
                    Field = nameof(model.TaxCode).ToCamelCase()
                });
            }
            if (!string.IsNullOrWhiteSpace(model.Fax) && data.FirstOrDefault(t => t.Fax == model.Fax) != null)
            {
                errors.Add(new ResponseError
                {
                    Message = Message.CustomerMessage.FAX_CODE_DUPLICATE,
                    Field = nameof(model.Fax).ToCamelCase()
                });
            }
            if (!string.IsNullOrWhiteSpace(model.BankAccount) && data.FirstOrDefault(t => t.BankAccount == model.BankAccount) != null)
            {
                errors.Add(new ResponseError
                {
                    Message = Message.CustomerMessage.BANK_ACCOUNT_DUPLICATE,
                    Field = nameof(model.BankAccount).ToCamelCase()
                });
            }
            if (!string.IsNullOrWhiteSpace(model.Email) && data.FirstOrDefault(t => t.Email == model.Email) != null)
            {
                errors.Add(new ResponseError
                {
                    Message = Message.CustomerMessage.CUSTOMER_EMAIL_DUPLICATE,
                    Field = nameof(model.Email).ToCamelCase()
                });
            }

            // Throw aggregated errors
            if (errors.Count > 0)
                throw new ValidationException(errors);

            existCustomer.CustomerCode = model.CustomerCode ?? existCustomer.CustomerCode;
            existCustomer.CustomerName = model.CustomerName ?? existCustomer.CustomerName;
            existCustomer.Phone = model.Phone ?? existCustomer.Phone;
            existCustomer.TaxCode = model.TaxCode ?? existCustomer.TaxCode;
            existCustomer.Fax = model.Fax ?? existCustomer.Fax;
            existCustomer.Email = model.Email ?? existCustomer.Email;
            existCustomer.DirectorName = model.DirectorName ?? existCustomer.DirectorName;
            existCustomer.Description = model.Description ?? existCustomer.Description;
            existCustomer.BankAccount = model.BankAccount ?? existCustomer.BankAccount;
            existCustomer.BankName = model.BankName ?? existCustomer.BankName;
            existCustomer.UpdatedAt = DateTime.Now;
            existCustomer.Updater = actionBy;

            _context.Update(existCustomer);
            _context.SaveChanges();
            _ = _cacheService.DeleteAsync(RedisCacheKey.CUSTOMER_CACHE_KEY);
            return existCustomer;
        }
    }
}
