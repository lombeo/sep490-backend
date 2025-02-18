using Moq;
using Sep490_Backend.Controllers;
using Sep490_Backend.DTO.Authen;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Services.AuthenService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTest_SEP490.AuthenUnitTest
{
    public class VerifyOTPUnitTest
    {
        private readonly Mock<IAuthenService> _mockAuthenService;
        private readonly AuthenController _controller;

        public VerifyOTPUnitTest()
        {
            _mockAuthenService = new Mock<IAuthenService>();
            _controller = new AuthenController(_mockAuthenService.Object);
        }

        [Fact]
        public async Task VerifyOTP_WithValidOTP_ShouldReturnSuccessResponse()
        {
            // Arrange
            var verifyOtpDto = new VerifyOtpDTO
            {
                OtpCode = "123456",
                UserId = 1,
                Reason = Sep490_Backend.Infra.Enums.ReasonOTP.SignUp
            };

            _mockAuthenService
                .Setup(service => service.VerifyOTP(It.IsAny<VerifyOtpDTO>()))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.VerifyOTP(verifyOtpDto);

            // Assert
            Assert.True(result.Success);
            Assert.True(result.Data);
            Assert.Equal(Message.AuthenMessage.VERIFY_OTP_SUCCESS, result.Message);

            _mockAuthenService.Verify(service => service.VerifyOTP(verifyOtpDto), Times.Once);
        }

        [Fact]
        public async Task VerifyOTP_WithInvalidOTP_ShouldReturnErrorResponse()
        {
            // Arrange
            var verifyOtpDto = new VerifyOtpDTO
            {
                OtpCode = "123456",
                UserId = 1,
                Reason = Sep490_Backend.Infra.Enums.ReasonOTP.SignUp
            };

            var expectedMessage = Message.AuthenMessage.INVALID_OTP;

            _mockAuthenService
                .Setup(service => service.VerifyOTP(It.IsAny<VerifyOtpDTO>()))
                .ThrowsAsync(new ApplicationException(expectedMessage));

            // Act
            var result = await _controller.VerifyOTP(verifyOtpDto);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(200, result.Code);
            Assert.Equal(expectedMessage, result.Message);

            _mockAuthenService.Verify(service => service.VerifyOTP(verifyOtpDto), Times.Once);
        }
    }
}
