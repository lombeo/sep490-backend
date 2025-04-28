using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Sep490_Backend.Controllers;
using Sep490_Backend.DTO.Admin;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Services.AdminService;
using Sep490_Backend.Services.DataService;
using System.Security.Claims;

namespace UnitTest_SEP490.Controllers
{
    public class AdminControllerTests
    {
        private readonly Mock<IAdminService> _mockAdminService;
        private readonly Mock<IDataService> _mockDataService;
        private readonly AdminController _controller;

        public AdminControllerTests()
        {
            _mockAdminService = new Mock<IAdminService>();
            _mockDataService = new Mock<IDataService>();
            _controller = new AdminController(_mockAdminService.Object, _mockDataService.Object);

            // Setup default user claims for testing with admin credentials
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Name, "adminuser"),
                new Claim(ClaimTypes.Role, "Administrator")
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

        #region ListUser Tests

        [Fact]
        public async Task ListUser_ReturnsSuccessResponse_WithUserList_WhenServiceSucceeds()
        {
            // Arrange
            var model = new AdminSearchUserDTO
            {
                KeyWord = "test",
                Role = "User",
                PageIndex = 1,
                PageSize = 10
            };

            var users = new List<User>
            {
                new User
                {
                    Id = 1,
                    Username = "testuser1",
                    Email = "test1@example.com",
                    Role = "User",
                    FullName = "Test User One",
                    Phone = "1234567890",
                    Gender = true,
                    Dob = DateTime.UtcNow.AddYears(-30),
                    IsVerify = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-10),
                    UpdatedAt = DateTime.UtcNow
                },
                new User
                {
                    Id = 2,
                    Username = "testuser2",
                    Email = "test2@example.com",
                    Role = "User",
                    FullName = "Test User Two",
                    Phone = "0987654321",
                    Gender = false,
                    Dob = DateTime.UtcNow.AddYears(-25),
                    IsVerify = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-5),
                    UpdatedAt = DateTime.UtcNow
                }
            };

            _mockDataService
                .Setup(s => s.ListUser(It.IsAny<AdminSearchUserDTO>()))
                .ReturnsAsync(users);

            // Act
            var result = await _controller.ListUser(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.AdminMessage.SEARCH_SUCCESS);
            result.Data.Should().NotBeNull();
            result.Data.Should().HaveCount(2);
            result.Meta.Should().NotBeNull();
            result.Meta.Total.Should().Be(model.Total);
            result.Meta.Index.Should().Be(model.PageIndex);
            result.Meta.PageSize.Should().Be(model.PageSize);

            // Verify that the service was called with the correct parameters
            _mockDataService.Verify(s => s.ListUser(It.Is<AdminSearchUserDTO>(m => 
                m.KeyWord == model.KeyWord && 
                m.Role == model.Role && 
                m.ActionBy == 1)), Times.Once);
        }

        [Fact]
        public async Task ListUser_ReturnsFailureResponse_WhenServiceThrowsException()
        {
            // Arrange
            var model = new AdminSearchUserDTO
            {
                KeyWord = "test",
                Role = "User",
                PageIndex = 1,
                PageSize = 10
            };

            _mockDataService
                .Setup(s => s.ListUser(It.IsAny<AdminSearchUserDTO>()))
                .ThrowsAsync(new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED));

            // Act
            var result = await _controller.ListUser(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(403);
            result.Message.Should().Be(Message.CommonMessage.NOT_ALLOWED);
        }

        [Fact]
        public async Task ListUser_SetsActionByFromUserId_BeforeCallingService()
        {
            // Arrange
            var model = new AdminSearchUserDTO
            {
                KeyWord = "test",
                Role = "User",
                PageIndex = 1,
                PageSize = 10
            };

            _mockDataService
                .Setup(s => s.ListUser(It.IsAny<AdminSearchUserDTO>()))
                .ReturnsAsync(new List<User>());

            // Act
            await _controller.ListUser(model);

            // Assert
            _mockDataService.Verify(s => s.ListUser(It.Is<AdminSearchUserDTO>(m => m.ActionBy == 1)), Times.Once);
        }

        #endregion

        #region DeleteUser Tests

        [Fact]
        public async Task DeleteUser_ReturnsSuccessResponse_WhenServiceSucceeds()
        {
            // Arrange
            int userId = 2;

            _mockAdminService
                .Setup(s => s.DeleteUser(userId, 1))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.DeleteUser(userId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.AdminMessage.DELETE_USER_SUCCESS);
            result.Data.Should().BeTrue();

            // Verify that the service was called with the correct parameters
            _mockAdminService.Verify(s => s.DeleteUser(userId, 1), Times.Once);
        }

        [Fact]
        public async Task DeleteUser_ReturnsFailureResponse_WhenServiceThrowsException()
        {
            // Arrange
            int userId = 1; // Attempting to delete own account

            _mockAdminService
                .Setup(s => s.DeleteUser(userId, 1))
                .ThrowsAsync(new ApplicationException(Message.AdminMessage.DELETE_USER_ERROR));

            // Act
            var result = await _controller.DeleteUser(userId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(200); // ApplicationException has code 200 in HandleException
            result.Message.Should().Be(Message.AdminMessage.DELETE_USER_ERROR);
        }

        #endregion

        #region CreateUser Tests

        [Fact]
        public async Task CreateUser_ReturnsSuccessResponse_WithCreatedUser_WhenServiceSucceeds()
        {
            // Arrange
            var model = new AdminCreateUserDTO
            {
                UserName = "newuser",
                Email = "newuser@example.com",
                Role = "User",
                FullName = "New User",
                Phone = "1234567890",
                Gender = true,
                Dob = DateTime.UtcNow.AddYears(-25),
                IsVerify = true
            };

            var createdUser = new User
            {
                Id = 3,
                Username = "newuser",
                Email = "newuser@example.com",
                Role = "User",
                FullName = "New User",
                Phone = "1234567890",
                Gender = true,
                Dob = DateTime.UtcNow.AddYears(-25),
                IsVerify = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Creator = 1
            };

            _mockAdminService
                .Setup(s => s.CreateUser(It.IsAny<AdminCreateUserDTO>(), 1))
                .ReturnsAsync(createdUser);

            // Act
            var result = await _controller.CreateUser(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.AdminMessage.CREATE_USER_SUCCESS);
            result.Data.Should().NotBeNull();
            result.Data.Id.Should().Be(createdUser.Id);
            result.Data.Username.Should().Be(createdUser.Username);
            result.Data.Email.Should().Be(createdUser.Email);
            result.Data.Role.Should().Be(createdUser.Role);

            // Verify that the service was called with the correct parameters
            _mockAdminService.Verify(s => s.CreateUser(It.Is<AdminCreateUserDTO>(m => 
                m.UserName == model.UserName && 
                m.Email == model.Email && 
                m.Role == model.Role), 1), Times.Once);
        }

        [Fact]
        public async Task CreateUser_ReturnsFailureResponse_WhenServiceThrowsException()
        {
            // Arrange
            var model = new AdminCreateUserDTO
            {
                UserName = "existinguser",
                Email = "existing@example.com",
                Role = "User",
                FullName = "Existing User",
                Phone = "1234567890",
                Gender = true,
                Dob = DateTime.UtcNow.AddYears(-25),
                IsVerify = true
            };

            _mockAdminService
                .Setup(s => s.CreateUser(It.IsAny<AdminCreateUserDTO>(), 1))
                .ThrowsAsync(new ApplicationException(Message.AdminMessage.CREATE_USER_ERROR));

            // Act
            var result = await _controller.CreateUser(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(200); // ApplicationException has code 200 in HandleException
            result.Message.Should().Be(Message.AdminMessage.CREATE_USER_ERROR);
        }

        [Fact]
        public async Task CreateUser_ReturnsFailureResponse_WhenInvalidRole()
        {
            // Arrange
            var model = new AdminCreateUserDTO
            {
                UserName = "newuser",
                Email = "newuser@example.com",
                Role = "InvalidRole", // Invalid role
                FullName = "New User",
                Phone = "1234567890",
                Gender = true,
                Dob = DateTime.UtcNow.AddYears(-25),
                IsVerify = true
            };

            _mockAdminService
                .Setup(s => s.CreateUser(It.IsAny<AdminCreateUserDTO>(), 1))
                .ThrowsAsync(new ArgumentException(Message.AdminMessage.INVALID_ROLE));

            // Act
            var result = await _controller.CreateUser(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(400); // ArgumentException has code 400 in HandleException
            result.Message.Should().Be(Message.AdminMessage.INVALID_ROLE);
        }

        #endregion

        #region UpdateUser Tests

        [Fact]
        public async Task UpdateUser_ReturnsSuccessResponse_WithUpdatedUser_WhenServiceSucceeds()
        {
            // Arrange
            var model = new AdminUpdateUserDTO
            {
                Id = 2,
                UserName = "updateduser",
                Email = "updated@example.com",
                Role = "User",
                FullName = "Updated User",
                Phone = "9876543210",
                Gender = false,
                Dob = DateTime.UtcNow.AddYears(-28),
                IsVerify = true,
                TeamId = 1
            };

            var updatedUser = new User
            {
                Id = 2,
                Username = "updateduser",
                Email = "updated@example.com",
                Role = "User",
                FullName = "Updated User",
                Phone = "9876543210",
                Gender = false,
                Dob = DateTime.UtcNow.AddYears(-28),
                IsVerify = true,
                TeamId = 1,
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                UpdatedAt = DateTime.UtcNow,
                Creator = 1,
                Updater = 1
            };

            _mockAdminService
                .Setup(s => s.UpdateUser(It.IsAny<AdminUpdateUserDTO>(), 1))
                .ReturnsAsync(updatedUser);

            // Act
            var result = await _controller.UpdateUser(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.AdminMessage.UPDATE_USER_SUCCESS);
            result.Data.Should().NotBeNull();
            result.Data.Id.Should().Be(updatedUser.Id);
            result.Data.Username.Should().Be(updatedUser.Username);
            result.Data.Email.Should().Be(updatedUser.Email);
            result.Data.Role.Should().Be(updatedUser.Role);
            result.Data.FullName.Should().Be(updatedUser.FullName);
            result.Data.Phone.Should().Be(updatedUser.Phone);
            result.Data.Gender.Should().Be(updatedUser.Gender);
            result.Data.Dob.Should().Be(updatedUser.Dob);
            result.Data.IsVerify.Should().Be(updatedUser.IsVerify);
            result.Data.TeamId.Should().Be(updatedUser.TeamId);

            // Verify that the service was called with the correct parameters
            _mockAdminService.Verify(s => s.UpdateUser(It.Is<AdminUpdateUserDTO>(m => 
                m.Id == model.Id && 
                m.UserName == model.UserName && 
                m.Email == model.Email && 
                m.Role == model.Role), 1), Times.Once);
        }

        [Fact]
        public async Task UpdateUser_ReturnsFailureResponse_WhenServiceThrowsException()
        {
            // Arrange
            var model = new AdminUpdateUserDTO
            {
                Id = 2,
                UserName = "existinguser",
                Email = "existing@example.com",
                Role = "InvalidRole", // Invalid role
                FullName = "Existing User",
                Phone = "1234567890",
                Gender = true,
                Dob = DateTime.UtcNow.AddYears(-25),
                IsVerify = true
            };

            _mockAdminService
                .Setup(s => s.UpdateUser(It.IsAny<AdminUpdateUserDTO>(), 1))
                .ThrowsAsync(new ArgumentException(Message.AdminMessage.INVALID_ROLE));

            // Act
            var result = await _controller.UpdateUser(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(400); // ArgumentException has code 400 in HandleException
            result.Message.Should().Be(Message.AdminMessage.INVALID_ROLE);
        }

        [Fact]
        public async Task UpdateUser_ReturnsFailureResponse_WhenUserNotFound()
        {
            // Arrange
            var model = new AdminUpdateUserDTO
            {
                Id = 999, // Non-existent user
                UserName = "nonexistent",
                Email = "nonexistent@example.com",
                Role = "User",
                FullName = "Non Existent User",
                Phone = "1234567890",
                Gender = true,
                Dob = DateTime.UtcNow.AddYears(-25),
                IsVerify = true
            };

            _mockAdminService
                .Setup(s => s.UpdateUser(It.IsAny<AdminUpdateUserDTO>(), 1))
                .ThrowsAsync(new KeyNotFoundException(Message.CommonMessage.NOT_FOUND));

            // Act
            var result = await _controller.UpdateUser(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(404);
            result.Message.Should().Be(Message.CommonMessage.NOT_FOUND);
        }

        #endregion
    }
} 