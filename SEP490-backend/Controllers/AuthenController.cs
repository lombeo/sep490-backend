using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sep490_Backend.DTO.AuthenDTO;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Services.AuthenService;
using System.Web;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Text.Json;

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
            return await HandleException(_authenService.SignUp(model), Message.AuthenMessage.SIGNUP_SUCCESS);
        }

        [AllowAnonymous]
        [HttpPost("verify-otp")]
        public async Task<ResponseDTO<bool>> VerifyOTP([FromBody] VerifyOtpDTO model)
        {
            if (UserId != 0)
            {
                model.UserId = UserId;
            }
            return await HandleException(_authenService.VerifyOTP(model), Message.AuthenMessage.VERIFY_OTP_SUCCESS);
        }

        [AllowAnonymous]
        [HttpPost("reset-password")]
        public async Task<ResponseDTO<int>> ForgetPassword([FromQuery] string email)
        {
            return await HandleException(_authenService.ForgetPassword(email), Message.AuthenMessage.FORGET_PASSWORD_SUCCESS);
        }

        [HttpPost("change-password")]
        public async Task<ResponseDTO<bool>> ChangePassword([FromBody] ChangePasswordDTO model)
        {
            return await HandleException(_authenService.ChangePassword(model, UserId), Message.AuthenMessage.CHANGE_PASSWORD_SUCCESS);
        }

        [AllowAnonymous]
        [HttpPost("sign-in")]
        public async Task<ResponseDTO<ReturnSignInDTO>> SignIn([FromBody] SignInDTO model)
        {
            return await HandleException(_authenService.SignIn(model), Message.AuthenMessage.SIGN_IN_SUCCESS);
        }


        [HttpPost("refresh")]
        public async Task<ResponseDTO<string>> Refresh([FromBody] string refreshToken)
        {
            return await HandleException(_authenService.Refresh(refreshToken), Message.AuthenMessage.REFRESH_TOKEN_SUCCESS);
        }


        [AllowAnonymous]
        [HttpPost("login-with-google")]
        public async Task<ResponseDTO<string>> LoginWithGoogle()
        {
            string clientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
            string redirectUri = "https://localhost:7233/sep490/authen/google-callback";

            var url = $@"https://accounts.google.com/o/oauth2/v2/auth?access_type=online&client_id={clientId}&redirect_uri={redirectUri}&response_type=code&scope=email&prompt=consent";

            return await HandleException(Task.FromResult(url), Message.AuthenMessage.REFRESH_TOKEN_SUCCESS);
        }

        [AllowAnonymous]
        [HttpPost("google-callback")]
        public async Task<ResponseDTO<ReturnSignInDTO>> GoogleCallBack([FromQuery] string authorizationCode)
        {
            return await HandleException(_authenService.GoogleCallback(authorizationCode), "An unexpected error occurred.");
        }
    }
}
