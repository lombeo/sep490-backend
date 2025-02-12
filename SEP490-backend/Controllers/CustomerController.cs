using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sep490_Backend.DTO.AdminDTO;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.CustomerDTO;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Services.CustomerService;

namespace Sep490_Backend.Controllers
{
    [ApiController]
    [Route(RouteApiConstant.BASE_PATH + "/customer")]
    [Authorize]
    public class CustomerController : BaseAPIController 
    {
        public readonly ICustomerService _customerSerivce;

        public CustomerController(ICustomerService customerSerivce)
        {
            _customerSerivce = customerSerivce;
        }

        [HttpGet("list-customer")]
        public async Task<ResponseDTO<List<Customer>>> GetListCustomer([FromQuery] CustomerSearchDTO model)
        {
            model.ActionBy = UserId;
            var result = await HandleException(_customerSerivce.GetListCustomer(model), Message.CustomerMessage.SEARCH_SUCCESS);
            result.Meta = new ResponseMeta() { Total = model.Total, Index = model.PageIndex, PageSize = model.PageSize };
            return result;
        }

        [HttpGet("detail-customer")]
        public async Task<ResponseDTO<Customer>> DetailCustomer([FromQuery] int customerId)
        {
            return await HandleException(_customerSerivce.GetDetailCustomer(customerId, UserId), Message.CustomerMessage.SEARCH_SUCCESS);
        }

        [HttpPost("create-customer")]
        public async Task<ResponseDTO<Customer>> CreateCustomer([FromBody] CustomerCreateDTO model)
        {
            return await HandleException(_customerSerivce.CreateCustomer(model, UserId), Message.CustomerMessage.CREATE_CUSTOMER_SUCCESS);
        }

        [HttpPut("update-customer")]
        public async Task<ResponseDTO<Customer>> UpdateCustomer([FromBody] Customer model)
        {
            return await HandleException(_customerSerivce.UpdateCustomer(model, UserId), Message.CustomerMessage.UPDATE_CUSTOMER_SUCCESS);
        }

        [HttpDelete("delete-customer")]
        public async Task<ResponseDTO<bool>> DeleteCustomer([FromQuery] int customerId)
        {
            return await HandleException(_customerSerivce.DeleteCustomer(customerId, UserId), Message.CustomerMessage.DELETE_CUSTOMER_SUCCESS);
        }
    }
}
