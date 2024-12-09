using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sep490_Backend.DTO.AuthenDTO;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Services.AuthenService;

namespace Sep490_Backend.Controllers
{
    [ApiController]
    [Route(RouteApiConstant.BASE_PATH + "/authen")]
    [Authorize]
    public class AuthenController : BaseAPIController
    {
        private readonly IAuthenService _authenService;

        public AuthenController(IAuthenService authenService)
        {
            _authenService = authenService;
        }

        [AllowAnonymous]
        [HttpPost("sign-up")]
        public async Task<ResponseDTO<bool>> SignUp([FromBody] SignUpDTO model)
        {
            return await HandleException(_authenService.SignUp(model));
        }

        [AllowAnonymous]
        [HttpPost("verify-otp")]
        public async Task<ResponseDTO<bool>> VerifyOTP([FromQuery] string otpCode)
        {
            return await HandleException(_authenService.VerifyOTP(UserId, otpCode));
        }
    }
}
