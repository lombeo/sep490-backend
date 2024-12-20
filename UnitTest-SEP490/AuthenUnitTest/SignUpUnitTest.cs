using Microsoft.AspNetCore.Mvc;
using Moq;
using Sep490_Backend.Controllers;
using Sep490_Backend.DTO.AuthenDTO;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.Services.AuthenService;
using Message = Sep490_Backend.Infra.Constants.Message;

namespace UnitTest_SEP490.AuthenUnitTest
{
    public class SignUpUnitTest
    {
        private readonly Mock<IAuthenService> _mockAuthenService;
        private readonly AuthenController _controller;

        public SignUpUnitTest()
        {
            _mockAuthenService = new Mock<IAuthenService>();
            _controller = new AuthenController(_mockAuthenService.Object);
        }

        [Fact]
        public async Task SignUp_WithValidInput_ShouldReturnSuccessResponse()
        {
            // Arrange
            var signUpDto = new SignUpDTO
            {
                Username = "validuser",
                Password = "ValidPass123!",
                Email = "validuser@example.com",
                FullName = "Valid User",
                Phone = "0123456789",
                Gender = true
            };

            _mockAuthenService
                .Setup(service => service.SignUp(signUpDto))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.SignUp(signUpDto);

            // Assert
            Assert.True(result.Success);
            Assert.True(result.Data);
            Assert.Equal(Message.AuthenMessage.SIGNUP_SUCCESS, result.Message);

            _mockAuthenService.Verify(service => service.SignUp(signUpDto), Times.Once);
        }

        [Fact]
        public async Task SignUp_WithMissingRequiredFields_ShouldReturnErrorResponse()
        {
            // Arrange
            var signUpDto = new SignUpDTO
            {
                Username = "",
                Password = "",
                Email = "",
                FullName = "Test User",
                Phone = "0123456789",
                Gender = true
            };

            var expectedMessage = Message.CommonMessage.MISSING_PARAM;

            _mockAuthenService
                .Setup(service => service.SignUp(It.IsAny<SignUpDTO>()))
                .ThrowsAsync(new ApplicationException(expectedMessage));

            // Act
            var result = await _controller.SignUp(signUpDto);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(200, result.Code);
            Assert.Equal(expectedMessage, result.Message);

            _mockAuthenService.Verify(service => service.SignUp(signUpDto), Times.Once);
        }

        [Fact]
        public async Task SignUp_WithDuplicateEmail_ShouldReturnErrorResponse()
        {
            // Arrange
            var signUpDto = new SignUpDTO
            {
                Username = "uniqueuser",
                Password = "ValidPass123!",
                Email = "duplicate@example.com",
                FullName = "Duplicate User",
                Phone = "0123456789",
                Gender = true
            };

            var expectedMessage = Message.AuthenMessage.EXIST_EMAIL;

            _mockAuthenService
                .Setup(service => service.SignUp(It.IsAny<SignUpDTO>()))
                .ThrowsAsync(new ApplicationException(expectedMessage));

            // Act
            var result = await _controller.SignUp(signUpDto);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(200, result.Code);
            Assert.Equal(expectedMessage, result.Message);

            _mockAuthenService.Verify(service => service.SignUp(signUpDto), Times.Once);
        }

        [Fact]
        public async Task SignUp_WithInvalidEmail_ShouldReturnErrorResponse()
        {
            // Arrange
            var signUpDto = new SignUpDTO
            {
                Username = "validuser",
                Password = "ValidPass123!",
                Email = "invalid-email",
                FullName = "Invalid Email User",
                Phone = "0123456789",
                Gender = true
            };

            var expectedMessage = Message.AuthenMessage.INVALID_EMAIL;

            _mockAuthenService
                .Setup(service => service.SignUp(It.IsAny<SignUpDTO>()))
                .ThrowsAsync(new ApplicationException(expectedMessage));

            // Act
            var result = await _controller.SignUp(signUpDto);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(200, result.Code);
            Assert.Equal(expectedMessage, result.Message);

            _mockAuthenService.Verify(service => service.SignUp(signUpDto), Times.Once);
        }

        [Fact]
        public async Task SignUp_WithInvalidPassword_ShouldReturnErrorResponse()
        {
            // Arrange
            var signUpDto = new SignUpDTO
            {
                Username = "validuser",
                Password = "123",
                Email = "validuser@example.com",
                FullName = "Invalid Password User",
                Phone = "0123456789",
                Gender = true
            };

            var expectedMessage = Message.AuthenMessage.INVALID_PASSWORD;

            _mockAuthenService
                .Setup(service => service.SignUp(It.IsAny<SignUpDTO>()))
                .ThrowsAsync(new ApplicationException(expectedMessage));

            // Act
            var result = await _controller.SignUp(signUpDto);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(200, result.Code);
            Assert.Equal(expectedMessage, result.Message);

            _mockAuthenService.Verify(service => service.SignUp(signUpDto), Times.Once);
        }

        [Fact]
        public async Task SignUp_WithDuplicateUsername_ShouldReturnErrorResponse()
        {
            // Arrange
            var signUpDto = new SignUpDTO
            {
                Username = "duplicateuser",
                Password = "ValidPass123!",
                Email = "unique@example.com",
                FullName = "Duplicate Username User",
                Phone = "0123456789",
                Gender = true
            };

            var expectedMessage = Message.AuthenMessage.EXIST_USERNAME;

            _mockAuthenService
                .Setup(service => service.SignUp(It.IsAny<SignUpDTO>()))
                .ThrowsAsync(new ApplicationException(expectedMessage));

            // Act
            var result = await _controller.SignUp(signUpDto);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(200, result.Code);
            Assert.Equal(expectedMessage, result.Message);

            _mockAuthenService.Verify(service => service.SignUp(signUpDto), Times.Once);
        }

        [Fact]
        public async Task SignUp_WithInvalidUsername_ShouldReturnErrorResponse()
        {
            // Arrange
            var signUpDto = new SignUpDTO
            {
                Username = "invalid user",
                Password = "ValidPass123!",
                Email = "validuser@example.com",
                FullName = "Invalid Username User",
                Phone = "0123456789",
                Gender = true
            };

            var expectedMessage = Message.AuthenMessage.INVALID_USERNAME;

            _mockAuthenService
                .Setup(service => service.SignUp(It.IsAny<SignUpDTO>()))
                .ThrowsAsync(new ApplicationException(expectedMessage));

            // Act
            var result = await _controller.SignUp(signUpDto);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(200, result.Code);
            Assert.Equal(expectedMessage, result.Message);

            _mockAuthenService.Verify(service => service.SignUp(signUpDto), Times.Once);
        }

        [Fact]
        public async Task SignUp_WithSystemError_ShouldReturnServerErrorResponse()
        {
            // Arrange
            var signUpDto = new SignUpDTO
            {
                Username = "validuser",
                Password = "ValidPass123!",
                Email = "validuser@example.com",
                FullName = "Valid User",
                Phone = "0123456789",
                Gender = true
            };

            _mockAuthenService
                .Setup(service => service.SignUp(It.IsAny<SignUpDTO>()))
                .ThrowsAsync(new Exception("Unexpected error"));

            // Act
            var result = await _controller.SignUp(signUpDto);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(500, result.Code);
            Assert.Equal(Message.CommonMessage.ERROR_HAPPENED, result.Message);

            _mockAuthenService.Verify(service => service.SignUp(signUpDto), Times.Once);
        }
    }
}