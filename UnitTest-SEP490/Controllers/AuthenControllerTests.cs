using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Sep490_Backend.Controllers;
using Sep490_Backend.DTO;
using Sep490_Backend.DTO.Authen;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Services.AuthenService;
using System.Security.Claims;

namespace UnitTest_SEP490.Controllers
{
    public class AuthenControllerTests
    {
        private readonly Mock<IAuthenService> _mockAuthenService;
        private readonly AuthenController _controller;

        public AuthenControllerTests()
        {
            _mockAuthenService = new Mock<IAuthenService>();
            _controller = new AuthenController(_mockAuthenService.Object);

            // Setup default user claims for testing
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Name, "testuser"),
                new Claim(ClaimTypes.Role, "User"),
                new Claim(ClaimTypes.Email, "test@example.com")
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            // Set ClaimsPrincipal to controller
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = claimsPrincipal
                }
            };
        }

        [Fact]
        public async Task VerifyOTP_ReturnsSuccessResponse_WhenServiceSucceeds()
        {
            // Arrange
            var model = new VerifyOtpDTO
            {
                UserId = 1,
                OtpCode = "12345678",
                Reason = Sep490_Backend.Infra.Enums.ReasonOTP.ForgetPassword
            };

            _mockAuthenService
                .Setup(s => s.VerifyOTP(It.IsAny<VerifyOtpDTO>()))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.VerifyOTP(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.AuthenMessage.VERIFY_OTP_SUCCESS);
            result.Data.Should().BeTrue();
        }

        [Fact]
        public async Task VerifyOTP_ReturnsFailureResponse_WhenServiceThrowsException()
        {
            // Arrange
            var model = new VerifyOtpDTO
            {
                UserId = 1,
                OtpCode = "12345678",
                Reason = Sep490_Backend.Infra.Enums.ReasonOTP.ForgetPassword
            };

            _mockAuthenService
                .Setup(s => s.VerifyOTP(It.IsAny<VerifyOtpDTO>()))
                .ThrowsAsync(new KeyNotFoundException(Message.CommonMessage.NOT_FOUND));

            // Act
            var result = await _controller.VerifyOTP(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(404);
            result.Message.Should().Be(Message.CommonMessage.NOT_FOUND);
        }

        [Fact]
        public async Task ForgetPassword_ReturnsSuccessResponse_WithUserId_WhenServiceSucceeds()
        {
            // Arrange
            string email = "test@example.com";
            int expectedUserId = 1;

            _mockAuthenService
                .Setup(s => s.ForgetPassword(email))
                .ReturnsAsync(expectedUserId);

            // Act
            var result = await _controller.ForgetPassword(email);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.AuthenMessage.FORGET_PASSWORD_SUCCESS);
            result.Data.Should().Be(expectedUserId);
        }

        [Fact]
        public async Task ForgetPassword_ReturnsFailureResponse_WhenServiceThrowsException()
        {
            // Arrange
            string email = "nonexistent@example.com";

            _mockAuthenService
                .Setup(s => s.ForgetPassword(email))
                .ThrowsAsync(new KeyNotFoundException(Message.CommonMessage.NOT_FOUND));

            // Act
            var result = await _controller.ForgetPassword(email);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(404);
            result.Message.Should().Be(Message.CommonMessage.NOT_FOUND);
        }

        [Fact]
        public async Task ChangePassword_ReturnsSuccessResponse_WhenServiceSucceeds()
        {
            // Arrange
            var model = new ChangePasswordDTO
            {
                CurrentPassword = "OldPassword123!",
                NewPassword = "NewPassword123!",
                ConfirmPassword = "NewPassword123!"
            };

            _mockAuthenService
                .Setup(s => s.ChangePassword(It.IsAny<ChangePasswordDTO>(), 1))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.ChangePassword(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.AuthenMessage.CHANGE_PASSWORD_SUCCESS);
            result.Data.Should().BeTrue();
        }

        [Fact]
        public async Task ChangePassword_ReturnsFailureResponse_WhenServiceThrowsException()
        {
            // Arrange
            var model = new ChangePasswordDTO
            {
                CurrentPassword = "WrongPassword",
                NewPassword = "NewPassword123!",
                ConfirmPassword = "NewPassword123!"
            };

            _mockAuthenService
                .Setup(s => s.ChangePassword(It.IsAny<ChangePasswordDTO>(), 1))
                .ThrowsAsync(new ArgumentException(Message.AuthenMessage.INVALID_CURRENT_PASSWORD));

            // Act
            var result = await _controller.ChangePassword(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(400);
            result.Message.Should().Be(Message.AuthenMessage.INVALID_CURRENT_PASSWORD);
        }

        [Fact]
        public async Task SignIn_ReturnsSuccessResponse_WithUserInfo_WhenServiceSucceeds()
        {
            // Arrange
            var model = new SignInDTO
            {
                Username = "testuser",
                Password = "Password123!"
            };

            var returnSignInDTO = new ReturnSignInDTO
            {
                UserId = 1,
                Username = "testuser",
                Email = "test@example.com",
                Role = "User",
                AccessToken = "valid.access.token",
                RefreshToken = "valid-refresh-token",
                IsVerify = true
            };

            _mockAuthenService
                .Setup(s => s.SignIn(It.IsAny<SignInDTO>()))
                .ReturnsAsync(returnSignInDTO);

            // Act
            var result = await _controller.SignIn(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.AuthenMessage.SIGN_IN_SUCCESS);
            result.Data.Should().NotBeNull();
            result.Data.UserId.Should().Be(1);
            result.Data.Username.Should().Be("testuser");
            result.Data.AccessToken.Should().Be("valid.access.token");
            result.Data.RefreshToken.Should().Be("valid-refresh-token");
        }

        [Fact]
        public async Task SignIn_ReturnsFailureResponse_WhenServiceThrowsException()
        {
            // Arrange
            var model = new SignInDTO
            {
                Username = "wronguser",
                Password = "WrongPassword"
            };

            _mockAuthenService
                .Setup(s => s.SignIn(It.IsAny<SignInDTO>()))
                .ThrowsAsync(new Microsoft.IdentityModel.Tokens.SecurityTokenException(Message.AuthenMessage.INVALID_CREDENTIALS));

            // Act
            var result = await _controller.SignIn(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(401);
            result.Message.Should().Be(Message.AuthenMessage.INVALID_CREDENTIALS);
        }

        [Fact]
        public async Task Refresh_ReturnsSuccessResponse_WithNewToken_WhenServiceSucceeds()
        {
            // Arrange
            string refreshToken = "valid-refresh-token";
            string newAccessToken = "new.access.token";

            _mockAuthenService
                .Setup(s => s.Refresh(refreshToken))
                .ReturnsAsync(newAccessToken);

            // Act
            var result = await _controller.Refresh(refreshToken);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.AuthenMessage.REFRESH_TOKEN_SUCCESS);
            result.Data.Should().Be(newAccessToken);
        }

        [Fact]
        public async Task Refresh_ReturnsFailureResponse_WhenServiceThrowsException()
        {
            // Arrange
            string refreshToken = "invalid-refresh-token";

            _mockAuthenService
                .Setup(s => s.Refresh(refreshToken))
                .ThrowsAsync(new ArgumentException(Message.AuthenMessage.INVALID_TOKEN));

            // Act
            var result = await _controller.Refresh(refreshToken);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(400);
            result.Message.Should().Be(Message.AuthenMessage.INVALID_TOKEN);
        }

        [Fact]
        public void UserProfileDetail_ReturnsSuccessResponse_WithUserData_WhenServiceSucceeds()
        {
            // Arrange
            int userId = 1;
            var userDTO = new UserDTO
            {
                UserId = 1,
                Username = "testuser",
                Email = "test@example.com",
                Role = "User",
                IsVerify = true,
                FullName = "Test User",
                Phone = "1234567890",
                Gender = true
            };

            _mockAuthenService
                .Setup(s => s.UserProfileDetail(userId))
                .Returns(userDTO);

            // Act
            var result = _controller.UserProfileDetail(userId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.AuthenMessage.GET_USER_DETAIL_SUCCESS);
            result.Data.Should().NotBeNull();
            result.Data.UserId.Should().Be(1);
            result.Data.Username.Should().Be("testuser");
            result.Data.Email.Should().Be("test@example.com");
            result.Data.FullName.Should().Be("Test User");
        }

        [Fact]
        public async Task UpdateProfile_ReturnsSuccessResponse_WithUpdatedData_WhenServiceSucceeds()
        {
            // Arrange
            var model = new UserUpdateProfileDTO
            {
                Username = "updateduser",
                FullName = "Updated User",
                Phone = "9876543210",
                Gender = false,
                Dob = new DateTime(1990, 1, 1),
                Address = "123 Updated Street"
            };

            var updatedUserDTO = new UserDTO
            {
                UserId = 1,
                Username = "updateduser",
                Email = "test@example.com",
                Role = "User",
                IsVerify = true,
                FullName = "Updated User",
                Phone = "9876543210",
                Gender = false,
                Dob = new DateTime(1990, 1, 1),
                Address = "123 Updated Street"
            };

            _mockAuthenService
                .Setup(s => s.UpdateProfile(1, It.IsAny<UserUpdateProfileDTO>()))
                .ReturnsAsync(updatedUserDTO);

            // Act
            var result = await _controller.UpdateProfile(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.AuthenMessage.UPDATE_PROFILE_SUCCESS);
            result.Data.Should().NotBeNull();
            result.Data.Username.Should().Be("updateduser");
            result.Data.FullName.Should().Be("Updated User");
            result.Data.Phone.Should().Be("9876543210");
            result.Data.Gender.Should().BeFalse();
        }

        [Fact]
        public async Task UpdateProfile_ReturnsFailureResponse_WhenServiceThrowsException()
        {
            // Arrange
            var model = new UserUpdateProfileDTO
            {
                Username = "existing username" // Assuming this username already exists for another user
            };

            var validationErrors = new List<ResponseError>
            {
                new ResponseError { Field = "username", Message = Message.AuthenMessage.EXIST_USERNAME }
            };

            _mockAuthenService
                .Setup(s => s.UpdateProfile(1, It.IsAny<UserUpdateProfileDTO>()))
                .ThrowsAsync(new Sep490_Backend.Controllers.ValidationException(validationErrors));

            // Act
            var result = await _controller.UpdateProfile(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(400);
            result.Errors.Should().HaveCount(1);
            result.Errors.First().Field.Should().Be("username");
            result.Errors.First().Message.Should().Be(Message.AuthenMessage.EXIST_USERNAME);
        }
    }
} 