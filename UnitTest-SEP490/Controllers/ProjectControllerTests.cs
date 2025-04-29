using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Sep490_Backend.Controllers;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.Project;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Services.DataService;
using Sep490_Backend.Services.ProjectService;
using System.Security.Claims;

namespace UnitTest_SEP490.Controllers
{
    public class ProjectControllerTests
    {
        private readonly Mock<IProjectService> _mockProjectService;
        private readonly Mock<IDataService> _mockDataService;
        private readonly Mock<ILogger<ProjectController>> _mockLogger;
        private readonly ProjectController _controller;

        public ProjectControllerTests()
        {
            _mockProjectService = new Mock<IProjectService>();
            _mockDataService = new Mock<IDataService>();
            _mockLogger = new Mock<ILogger<ProjectController>>();
            _controller = new ProjectController(_mockProjectService.Object, _mockDataService.Object, _mockLogger.Object);

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
        public async Task List_ReturnsSuccessResponse_WithProjects()
        {
            // Arrange
            var searchModel = new SearchProjectDTO
            {
                KeyWord = "Test",
                CustomerId = 1,
                Status = Sep490_Backend.Infra.Enums.ProjectStatusEnum.InProgress
            };

            var projects = new List<ProjectDTO>
            {
                new ProjectDTO
                {
                    Id = 1,
                    ProjectCode = "P001",
                    ProjectName = "Test Project 1",
                    Customer = new Customer { Id = 1, CustomerName = "Customer 1" },
                    Status = Sep490_Backend.Infra.Enums.ProjectStatusEnum.InProgress
                },
                new ProjectDTO
                {
                    Id = 2,
                    ProjectCode = "P002",
                    ProjectName = "Test Project 2",
                    Customer = new Customer { Id = 1, CustomerName = "Customer 1" },
                    Status = Sep490_Backend.Infra.Enums.ProjectStatusEnum.InProgress
                }
            };

            _mockDataService
                .Setup(s => s.ListProject(It.IsAny<SearchProjectDTO>()))
                .ReturnsAsync(projects);

            // Act
            var result = await _controller.List(searchModel);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.ProjectMessage.SEARCH_SUCCESS);
            result.Data.Should().NotBeNull();
            result.Data.Should().HaveCount(2);
            result.Data[0].ProjectCode.Should().Be("P001");
            result.Data[1].ProjectCode.Should().Be("P002");
            result.Meta.Should().NotBeNull();
        }

        [Fact]
        public async Task List_ReturnsEmptyList_WhenNoProjectsFound()
        {
            // Arrange
            var searchModel = new SearchProjectDTO { KeyWord = "NonExistent" };

            _mockDataService
                .Setup(s => s.ListProject(It.IsAny<SearchProjectDTO>()))
                .ReturnsAsync(new List<ProjectDTO>());

            // Act
            var result = await _controller.List(searchModel);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Data.Should().BeEmpty();
        }

        [Fact]
        public async Task List_ReturnsFailureResponse_WhenServiceThrowsException()
        {
            // Arrange
            var searchModel = new SearchProjectDTO();

            _mockDataService
                .Setup(s => s.ListProject(It.IsAny<SearchProjectDTO>()))
                .ThrowsAsync(new Exception(Message.CommonMessage.ERROR_HAPPENED));

            // Act
            var result = await _controller.List(searchModel);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(500);
            result.Message.Should().Be(Message.CommonMessage.ERROR_HAPPENED);
        }

        [Fact]
        public async Task Save_ReturnsSuccessResponse_WithSavedProject()
        {
            // Arrange
            var model = new SaveProjectDTO
            {
                Id = 0,
                ProjectCode = "P003",
                ProjectName = "New Project",
                CustomerId = 1,
                ConstructType = "Building",
                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddMonths(6),
                Budget = 100000
            };

            var savedProject = new ProjectDTO
            {
                Id = 3,
                ProjectCode = "P003",
                ProjectName = "New Project",
                Customer = new Customer { Id = 1, CustomerName = "Customer 1" },
                ConstructType = "Building",
                StartDate = model.StartDate,
                EndDate = model.EndDate,
                Budget = 100000,
                Status = Sep490_Backend.Infra.Enums.ProjectStatusEnum.Planning
            };

            _mockProjectService
                .Setup(s => s.Save(It.IsAny<SaveProjectDTO>(), It.IsAny<int>()))
                .ReturnsAsync(savedProject);

            // Act
            var result = await _controller.Save(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.ProjectMessage.SAVE_SUCCESS);
            result.Data.Should().NotBeNull();
            result.Data.Id.Should().Be(3);
            result.Data.ProjectCode.Should().Be("P003");
            result.Data.ProjectName.Should().Be("New Project");
        }

        [Fact]
        public async Task Save_ReturnsFailureResponse_WhenValidationFails()
        {
            // Arrange
            var model = new SaveProjectDTO
            {
                Id = 0,
                ProjectCode = "P003",
                ProjectName = "New Project",
                CustomerId = 1,
                ConstructType = "Building"
            };

            var errors = new List<ResponseError>
            {
                new ResponseError { Field = "projectCode", Message = "Project code already exists" }
            };

            _mockProjectService
                .Setup(s => s.Save(It.IsAny<SaveProjectDTO>(), It.IsAny<int>()))
                .ThrowsAsync(new Sep490_Backend.Controllers.ValidationException(errors));

            // Act
            var result = await _controller.Save(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Errors.Should().NotBeEmpty();
        }

        [Fact]
        public async Task Delete_ReturnsSuccessResponse_WhenProjectDeleted()
        {
            // Arrange
            int projectId = 1;

            _mockProjectService
                .Setup(s => s.Delete(projectId, It.IsAny<int>()))
                .ReturnsAsync(projectId);

            // Act
            var result = await _controller.Delete(projectId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.ProjectMessage.DELETE_SUCCESS);
            result.Data.Should().Be(projectId);
        }

        [Fact]
        public async Task Delete_ReturnsFailureResponse_WhenProjectNotFound()
        {
            // Arrange
            int projectId = 999;

            _mockProjectService
                .Setup(s => s.Delete(projectId, It.IsAny<int>()))
                .ThrowsAsync(new KeyNotFoundException(Message.CommonMessage.NOT_FOUND));

            // Act
            var result = await _controller.Delete(projectId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(404);
            result.Message.Should().Be(Message.CommonMessage.NOT_FOUND);
        }

        [Fact]
        public async Task ListProjectStatus_ReturnsSuccessResponse_WithStatusCounts()
        {
            // Arrange
            var statusCounts = new ListProjectStatusDTO
            {
                ReceiveRequest = 2,
                Planning = 3,
                InProgress = 5,
                Completed = 1,
                Paused = 0,
                Closed = 2,
                Total = 13
            };

            _mockProjectService
                .Setup(s => s.ListProjectStatus(It.IsAny<int>()))
                .ReturnsAsync(statusCounts);

            // Act
            var result = await _controller.ListProjectStatus();

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.ProjectMessage.GET_LIST_STATUS_SUCCESS);
            result.Data.Should().NotBeNull();
            result.Data.ReceiveRequest.Should().Be(2);
            result.Data.Planning.Should().Be(3);
            result.Data.InProgress.Should().Be(5);
            result.Data.Completed.Should().Be(1);
            result.Data.Paused.Should().Be(0);
            result.Data.Closed.Should().Be(2);
            result.Data.Total.Should().Be(13);
        }

        [Fact]
        public async Task Detail_ReturnsSuccessResponse_WithProjectDetails()
        {
            // Arrange
            int projectId = 1;
            var projectDetails = new ProjectDTO
            {
                Id = projectId,
                ProjectCode = "P001",
                ProjectName = "Test Project",
                Customer = new Customer { Id = 1, CustomerName = "Customer 1" },
                ConstructType = "Building",
                Location = "Test Location",
                Area = "100m²",
                Purpose = "Test Purpose",
                TechnicalReqs = "Test Requirements",
                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddMonths(6),
                Budget = 100000,
                Status = Sep490_Backend.Infra.Enums.ProjectStatusEnum.InProgress,
                Description = "Test Description"
            };

            _mockProjectService
                .Setup(s => s.Detail(projectId, It.IsAny<int>()))
                .ReturnsAsync(projectDetails);

            // Act
            var result = await _controller.Detail(projectId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.ProjectMessage.GET_DETAIL_SUCCESS);
            result.Data.Should().NotBeNull();
            result.Data.Id.Should().Be(projectId);
            result.Data.ProjectCode.Should().Be("P001");
            result.Data.ProjectName.Should().Be("Test Project");
            result.Data.ConstructType.Should().Be("Building");
            result.Data.Customer.Should().NotBeNull();
            result.Data.Customer.Id.Should().Be(1);
            result.Data.Customer.CustomerName.Should().Be("Customer 1");
        }

        [Fact]
        public async Task Detail_ReturnsFailureResponse_WhenProjectNotFound()
        {
            // Arrange
            int projectId = 999;

            _mockProjectService
                .Setup(s => s.Detail(projectId, It.IsAny<int>()))
                .ThrowsAsync(new KeyNotFoundException(Message.CommonMessage.NOT_FOUND));

            // Act
            var result = await _controller.Detail(projectId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(404);
            result.Message.Should().Be(Message.CommonMessage.NOT_FOUND);
        }
    }
} 