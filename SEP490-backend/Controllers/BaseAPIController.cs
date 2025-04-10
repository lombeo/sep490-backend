using Sep490_Backend.DTO.Common;
using Sep490_Backend.Infra.Constants;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

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

        public async Task<ResponseDTO<T>> HandleException<T>(Task<T> task, string successMessage = Message.CommonMessage.ACTION_SUCCESS)
        {
            try
            {
                var data = await task;
                return new ResponseDTO<T>
                {
                    Success = true,
                    Data = data,
                    Message = successMessage
                };
            }
            catch (ArgumentNullException ex)
            {
                Serilog.Log.Debug(ex, "ArgumentNullException: {Message}", ex.Message);
                return new ResponseDTO<T>
                {
                    Success = false,
                    Code = 400,
                    Message = ex.Message
                };
            }
            catch (ArgumentException ex)
            {
                Serilog.Log.Debug(ex, "ArgumentException: {Message}", ex.Message);
                return new ResponseDTO<T>
                {
                    Success = false,
                    Code = 400,
                    Message = ex.Message
                };
            }
            catch (ValidationException ex)
            {
                Serilog.Log.Debug(ex, "ValidationException: {Message}", ex.Message);
                return new ResponseDTO<T>
                {
                    Success = false,
                    Code = 400,
                    Message = ex.Message,
                    Errors = ex.Errors
                };
            }
            catch (InvalidOperationException ex)
            {
                Serilog.Log.Debug(ex, "InvalidOperationException: {Message}", ex.Message);
                return new ResponseDTO<T>
                {
                    Success = false,
                    Code = 400,
                    Message = ex.Message
                };
            }
            catch (FormatException ex)
            {
                Serilog.Log.Debug(ex, "FormatException: {Message}", ex.Message);
                return new ResponseDTO<T>
                {
                    Success = false,
                    Code = 400,
                    Message = ex.Message
                };
            }
            catch (SecurityTokenException ex)
            {
                Serilog.Log.Debug(ex, "SecurityTokenException: {Message}", ex.Message);
                return new ResponseDTO<T>
                {
                    Success = false,
                    Code = 401,
                    Message = ex.Message
                };
            }
            // --- Kết thúc phần xử lý lỗi xác thực ---
            catch (TimeoutException ex)
            {
                Serilog.Log.Debug(ex, "TimeoutException: {Message}", ex.Message);
                return new ResponseDTO<T>
                {
                    Success = false,
                    Code = 408,
                    Message = ex.Message
                };
            }
            catch (KeyNotFoundException ex)
            {
                Serilog.Log.Debug(ex, "KeyNotFoundException: {Message}", ex.Message);
                return new ResponseDTO<T>
                {
                    Success = false,
                    Code = 404,
                    Message = ex.Message
                };
            }
            catch (UnauthorizedAccessException ex)
            {
                Serilog.Log.Debug(ex, "UnauthorizedAccessException: {Message}", ex.Message);
                return new ResponseDTO<T>
                {
                    Success = false,
                    Code = 403,
                    Message = ex.Message
                };
            }
            catch (ApplicationException ex)
            {
                Serilog.Log.Debug(ex, "ApplicationException: {Message}", ex.Message);
                return new ResponseDTO<T>
                {
                    Success = false,
                    Code = 200, // Theo mẫu ban đầu; cân nhắc điều chỉnh nếu cần
                    Message = ex.Message
                };
            }
            catch (NullReferenceException ex)
            {
                Serilog.Log.Error(ex, "NullReferenceException: {Message}", ex.Message);
                return new ResponseDTO<T>
                {
                    Success = false,
                    Code = 500,
                    Message = Message.CommonMessage.ERROR_HAPPENED
                };
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Unhandled exception: {Message}", ex.Message);
                return new ResponseDTO<T>
                {
                    Success = false,
                    Code = 500,
                    Message = Message.CommonMessage.ERROR_HAPPENED
                };
            }
        }
    }

    public class ValidationException : Exception
    {
        public List<ResponseError> Errors { get; }

        public ValidationException(List<ResponseError> errors)
            : base("One or more validation errors occurred.")
        {
            Errors = errors;
        }
    }
}
