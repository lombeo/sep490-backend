using Sep490_Backend.DTO.Common;
using Sep490_Backend.Infra.Constants;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Sep490_Backend.Controllers
{
    [ApiController]
    public class BaseAPIController : ControllerBase
    {
        public BaseAPIController()
        {
        }

		public int UserId
		{
			get
			{
				return int.Parse(User?.Claims?.FirstOrDefault(t => t.Type == ClaimTypes.NameIdentifier)?.Value ?? "0");
			}
		}

		public async Task<ResponseDTO<T>> HandleException<T>(Task<T> task, string successMessage = "Operation successful")
        {
            try
            {
                var data = await task;
                return new ResponseDTO<T>() { Success = true, Data = data, Message = successMessage };
            }
            catch (ApplicationException ex)
            {
                //Serilog.Log.Debug(ex, ex.Message);
                return new ResponseDTO<T>() { Success = false, Code = 200, Message = ex.Message };
            }
            catch (KeyNotFoundException ex)
            {
                //Serilog.Log.Debug(ex, ex.Message);
                return new ResponseDTO<T>() { Success = false, Code = 404, Message = ex.Message };
            }
            catch (UnauthorizedAccessException ex)
            {
                return new ResponseDTO<T>() { Success = false, Code = 403, Message = ex.Message };
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, ex.Message);
                return new ResponseDTO<T>() { Success = false, Code = 500, Message = Message.CommonMessage.ERROR_HAPPENED };
            }
        }
    }
}
