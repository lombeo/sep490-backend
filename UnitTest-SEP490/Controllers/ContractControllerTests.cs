using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Sep490_Backend.Controllers;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.Contract;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Services.ContractService;
using System.Security.Claims;

namespace UnitTest_SEP490.Controllers
{
    public class ContractControllerTests
    {
        private readonly Mock<IContractService> _mockContractService;
        private readonly ContractController _controller;

        public ContractControllerTests()
        {
            _mockContractService = new Mock<IContractService>();
            _controller = new ContractController(_mockContractService.Object);

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
        public async Task DeleteContract_ReturnsSuccessResponse_WhenContractDeleted()
        {
            // Arrange
            int projectId = 1;

            _mockContractService
                .Setup(s => s.Delete(projectId, It.IsAny<int>()))
                .ReturnsAsync(projectId);

            // Act
            var result = await _controller.DeleteContract(projectId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.ContractMessage.DELETE_SUCCESS);
            result.Data.Should().Be(projectId);
        }

        [Fact]
        public async Task DeleteContract_ReturnsFailureResponse_WhenContractNotFound()
        {
            // Arrange
            int projectId = 999;

            _mockContractService
                .Setup(s => s.Delete(projectId, It.IsAny<int>()))
                .ThrowsAsync(new KeyNotFoundException(Message.CommonMessage.NOT_FOUND));

            // Act
            var result = await _controller.DeleteContract(projectId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(404);
            result.Message.Should().Be(Message.CommonMessage.NOT_FOUND);
        }

        [Fact]
        public async Task DeleteContract_ReturnsFailureResponse_WhenServiceThrowsException()
        {
            // Arrange
            int projectId = 1;

            _mockContractService
                .Setup(s => s.Delete(projectId, It.IsAny<int>()))
                .ThrowsAsync(new Exception(Message.CommonMessage.ERROR_HAPPENED));

            // Act
            var result = await _controller.DeleteContract(projectId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(500);
            result.Message.Should().Be(Message.CommonMessage.ERROR_HAPPENED);
        }

        [Fact]
        public async Task SaveContract_ReturnsSuccessResponse_WithSavedContract()
        {
            // Arrange
            var model = new SaveContractDTO
            {
                Id = 0,
                ContractCode = "C001",
                ContractName = "Test Contract",
                ProjectId = 1,
                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddMonths(3),
                EstimatedDays = 90,
                Status = Sep490_Backend.Infra.Enums.ContractStatusEnum.Active,
                Tax = 10,
                SignDate = DateTime.Now
            };

            var savedContract = new ContractDTO
            {
                Id = 1,
                ContractCode = "C001",
                ContractName = "Test Contract",
                ProjectId = 1,
                Project = new Sep490_Backend.DTO.Project.ProjectDTO { Id = 1, ProjectName = "Test Project" },
                StartDate = model.StartDate,
                EndDate = model.EndDate,
                EstimatedDays = 90,
                Status = Sep490_Backend.Infra.Enums.ContractStatusEnum.Active,
                Tax = 10,
                SignDate = model.SignDate
            };

            _mockContractService
                .Setup(s => s.Save(It.IsAny<SaveContractDTO>()))
                .ReturnsAsync(savedContract);

            // Act
            var result = await _controller.SaveContract(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.ContractMessage.SAVE_SUCCESS);
            result.Data.Should().NotBeNull();
            result.Data.Id.Should().Be(1);
            result.Data.ContractCode.Should().Be("C001");
            result.Data.ContractName.Should().Be("Test Contract");
            _mockContractService.Verify(s => s.Save(It.Is<SaveContractDTO>(dto => dto.ActionBy == 1)), Times.Once);
        }

        [Fact]
        public async Task SaveContract_ReturnsFailureResponse_WhenProjectAlreadyHasContract()
        {
            // Arrange
            var model = new SaveContractDTO
            {
                Id = 0,
                ContractCode = "C001",
                ContractName = "Test Contract",
                ProjectId = 1,
                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddMonths(3),
                EstimatedDays = 90,
                Status = Sep490_Backend.Infra.Enums.ContractStatusEnum.Active,
                Tax = 10,
                SignDate = DateTime.Now
            };

            _mockContractService
                .Setup(s => s.Save(It.IsAny<SaveContractDTO>()))
                .ThrowsAsync(new InvalidOperationException(Message.ContractMessage.PROJECT_ALREADY_HAS_CONTRACT));

            // Act
            var result = await _controller.SaveContract(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(400);
            result.Message.Should().Be(Message.ContractMessage.PROJECT_ALREADY_HAS_CONTRACT);
        }

        [Fact]
        public async Task DetailContract_ReturnsSuccessResponse_WithContractDetails()
        {
            // Arrange
            int projectId = 1;
            var contractDetails = new ContractDTO
            {
                Id = 1,
                ContractCode = "C001",
                ContractName = "Test Contract",
                ProjectId = projectId,
                Project = new Sep490_Backend.DTO.Project.ProjectDTO { Id = projectId, ProjectName = "Test Project" },
                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddMonths(3),
                EstimatedDays = 90,
                Status = Sep490_Backend.Infra.Enums.ContractStatusEnum.Active,
                Tax = 10,
                SignDate = DateTime.Now,
                ContractDetails = new List<ContractDetailDTO>
                {
                    new ContractDetailDTO 
                    { 
                        ContractId = 1, 
                        WorkCode = "W001", 
                        Index = "1", 
                        WorkName = "Foundation Work",
                        Unit = "m²",
                        Quantity = 100,
                        UnitPrice = 50,
                        Total = 5000
                    }
                }
            };

            _mockContractService
                .Setup(s => s.Detail(projectId, It.IsAny<int>()))
                .ReturnsAsync(contractDetails);

            // Act
            var result = await _controller.DetailContract(projectId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.ContractMessage.SEARCH_SUCCESS);
            result.Data.Should().NotBeNull();
            result.Data.Id.Should().Be(1);
            result.Data.ContractCode.Should().Be("C001");
            result.Data.ProjectId.Should().Be(projectId);
            result.Data.ContractDetails.Should().HaveCount(1);
            result.Data.ContractDetails[0].WorkCode.Should().Be("W001");
        }

        [Fact]
        public async Task DetailContract_ReturnsFailureResponse_WhenContractNotFound()
        {
            // Arrange
            int projectId = 999;

            _mockContractService
                .Setup(s => s.Detail(projectId, It.IsAny<int>()))
                .ThrowsAsync(new KeyNotFoundException(Message.CommonMessage.NOT_FOUND));

            // Act
            var result = await _controller.DetailContract(projectId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(404);
            result.Message.Should().Be(Message.CommonMessage.NOT_FOUND);
        }

        [Fact]
        public async Task DetailContract_ReturnsFailureResponse_WhenUserNotAuthorized()
        {
            // Arrange
            int projectId = 1;

            _mockContractService
                .Setup(s => s.Detail(projectId, It.IsAny<int>()))
                .ThrowsAsync(new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED_PROJECT));

            // Act
            var result = await _controller.DetailContract(projectId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(403);
            result.Message.Should().Be(Message.CommonMessage.NOT_ALLOWED_PROJECT);
        }
    }
} 