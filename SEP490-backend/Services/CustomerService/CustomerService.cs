using Sep490_Backend.DTO.AdminDTO;
using Sep490_Backend.DTO.CustomerDTO;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Services.AuthenService;
using Sep490_Backend.Services.EmailService;
using Sep490_Backend.Services.HelperService;

namespace Sep490_Backend.Services.CustomerService
{
    public interface ICustomerService
    {
        Task<List<Customer>> GetListCustomer(CustomerSearchDTO model);
        Task<CustomerDetailDTO> GetDetailCustomer(int customerId);
        Task<bool> DeleteCustomer(int userId, int actionBy);
        Task<bool> CreateCustomer(AdminCreateUserDTO model, int actionBy);
        Task<bool> UpdateCustomer(AdminUpdateUserDTO model, int actionBy);
    }

    public class CustomerService : ICustomerService
    {
        private readonly BackendContext _context;
        private readonly IAuthenService _authenService;
        private readonly IEmailService _emailService;
        private readonly IHelperService _helperService;
    
        public CustomerService(BackendContext context, IAuthenService authenService, IEmailService emailService, IHelperService helperService)
        {
            _context = context;
            _authenService = authenService;
            _emailService = emailService;
            _helperService = helperService;
        }

        public Task<bool> CreateCustomer(AdminCreateUserDTO model, int actionBy)
        {
            throw new NotImplementedException();
        }

        public Task<bool> DeleteCustomer(int userId, int actionBy)
        {
            throw new NotImplementedException();
        }

        public Task<CustomerDetailDTO> GetDetailCustomer(int customerId)
        {
            throw new NotImplementedException();
        }

        public Task<List<Customer>> GetListCustomer(CustomerSearchDTO model)
        {
            throw new NotImplementedException();
        }

        public Task<bool> UpdateCustomer(AdminUpdateUserDTO model, int actionBy)
        {
            throw new NotImplementedException();
        }
    }
}
