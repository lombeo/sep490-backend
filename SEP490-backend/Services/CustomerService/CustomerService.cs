using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.Office2013.Excel;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using Sep490_Backend.DTO.AdminDTO;
using Sep490_Backend.DTO.CustomerDTO;
using Sep490_Backend.DTO.SiteSurveyDTO;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Services.AuthenService;
using Sep490_Backend.Services.CacheService;
using Sep490_Backend.Services.EmailService;
using Sep490_Backend.Services.HelperService;
using System.ComponentModel.Design;
using System.Text.RegularExpressions;

namespace Sep490_Backend.Services.CustomerService
{
    public interface ICustomerService
    {
        Task<List<Customer>> GetListCustomer(CustomerSearchDTO model);
        Task<Customer> GetDetailCustomer(int customerId, int actionBy);
        Task<bool> DeleteCustomer(int customerId, int actionBy);
        Task<bool> CreateCustomer(CustomerCreateDTO model, int actionBy);
        Task<bool> UpdateCustomer(Customer model, int actionBy);
    }

    public class CustomerService : ICustomerService
    {
        private readonly BackendContext _context;
        private readonly IAuthenService _authenService;
        private readonly IHelperService _helperService;
        private readonly ICacheService _cacheService;


        public CustomerService(BackendContext context, IAuthenService authenService, IEmailService emailService, IHelperService helperService, ICacheService cacheService)
        {
            _context = context;
            _authenService = authenService;
            _helperService = helperService;
            _cacheService = cacheService;
        }

        public async Task<List<Customer>> GetListCustomer(CustomerSearchDTO model)
        {
            if (!_helperService.IsInRole(model.ActionBy, new List<string> { RoleConstValue.BUSINESS_EMPLOYEE, RoleConstValue.EXECUTIVE_BOARD }))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            string cacheKey = RedisCacheKey.CUSTOMER_CACHE_KEY;
            var customerCacheList = await _cacheService.GetAsync<List<Customer>>(cacheKey);
            if(customerCacheList == null)
            {
                customerCacheList = await _context.Customers.Where(c => !c.Deleted).ToListAsync();
                _ = _cacheService.SetAsync(cacheKey, customerCacheList);
            }
            if (!string.IsNullOrWhiteSpace(model.CustomerName))
            {
                customerCacheList = customerCacheList.Where(t => t.CustomerName.ToLower().Trim().Contains(model.CustomerName.ToLower().Trim())).ToList();
            }
            if (!string.IsNullOrWhiteSpace(model.CustomerCode))
            {
                customerCacheList = customerCacheList.Where(t => t.CustomerCode.ToLower().Trim().Contains(model.CustomerCode.ToLower().Trim())).ToList();
            }
            if (!string.IsNullOrWhiteSpace(model.Phone))
            {
                customerCacheList = customerCacheList.Where(t => t.Phone.ToLower().Trim().Contains(model.Phone.ToLower().Trim())).ToList();
            }
            model.Total = customerCacheList.Count();
            if (model.PageSize > 0)
            {
                customerCacheList = customerCacheList.Skip(model.Skip).Take(model.PageSize).ToList();
            }
            return customerCacheList;
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

        public async Task<bool> CreateCustomer(CustomerCreateDTO model, int actionBy)
        {
            if (!_helperService.IsInRole(actionBy, new List<string> { RoleConstValue.BUSINESS_EMPLOYEE }))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            if (model == null || string.IsNullOrWhiteSpace(model.CustomerCode) || string.IsNullOrWhiteSpace(model.CustomerName))
            {
                throw new ArgumentException(Message.CommonMessage.INVALID_FORMAT);
            }
            if (!Regex.IsMatch(model.Email, PatternConst.EMAIL_PATTERN))
            {
                throw new ArgumentException(Message.AuthenMessage.INVALID_EMAIL);
            }
            
            var customer = new Customer
            {
                CustomerCode = model.CustomerCode,
                CustomerName = model.CustomerName,
                Email = model.Email ?? "",
                Phone = model.Phone ?? "",
                TaxCode = model.TaxCode ?? "",
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

            return true;
        }

        public async Task<bool> UpdateCustomer(Customer model, int actionBy)
        {
            if (!_helperService.IsInRole(actionBy, RoleConstValue.BUSINESS_EMPLOYEE))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            var existCustomer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == model.Id);
            if (existCustomer == null)
            {
                throw new KeyNotFoundException(Message.CustomerMessage.CUSTOMER_NOT_FOUND);
            }
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
            return true;
        }

      
    }
}
