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
        public async Task<ResponseDTO<bool>> VerifyOTP([FromBody] VerifyOtpDTO model)
        {
            if(UserId != 0)
            {
                model.UserId = UserId;
            }
            return await HandleException(_authenService.VerifyOTP(model));
        }

        [AllowAnonymous]
        [HttpPost("reset-password")]
        public async Task<ResponseDTO<int>> ForgetPassword ([FromBody] string email)
        {
            return await HandleException(_authenService.ForgetPassword(email));
        }

        [AllowAnonymous]
        [HttpPost("change-password")]
        public async Task<ResponseDTO<bool>> ChangePassword([FromBody] ChangePasswordDTO model)
        {
            if(UserId != 0)
            {
                model.UserId = UserId;
            }
            return await HandleException(_authenService.ChangePassword(model));
        }

        [AllowAnonymous]
        [HttpPost("sign-in")]
        public async Task<ResponseDTO<ReturnSignInDTO>> SignIn([FromBody] SignInDTO model)
        {
            return await HandleException(_authenService.SignIn(model));
        }

        [HttpPost("refresh")]
        public async Task<ResponseDTO<string>> Refresh([FromBody] string refreshToken)
        {
            return await HandleException(_authenService.Refresh(refreshToken));
        }
    }
}
