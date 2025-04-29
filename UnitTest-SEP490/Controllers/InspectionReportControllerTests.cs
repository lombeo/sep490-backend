using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Sep490_Backend.Controllers;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.InspectionReport;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Enums;
using Sep490_Backend.Services.InspectionReportService;
using System.Security.Claims;

namespace UnitTest_SEP490.Controllers
{
    public class InspectionReportControllerTests
    {
        private readonly Mock<IInspectionReportService> _mockInspectionReportService;
        private readonly InspectionReportController _controller;

        public InspectionReportControllerTests()
        {
            _mockInspectionReportService = new Mock<IInspectionReportService>();
            _controller = new InspectionReportController(_mockInspectionReportService.Object);

            // Setup default user claims for testing
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Name, "testuser"),
                new Claim(ClaimTypes.Role, "QualityAssurance"),
                new Claim(ClaimTypes.Email, "test@example.com")
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            // Set ClaimsPrincipal to controller
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = claimsPrincipal
                }
            };
        }

        [Fact]
        public async Task List_ReturnsSuccessResponse_WithInspectionReports()
        {
            // Arrange
            var searchModel = new SearchInspectionReportDTO
            {
                ProjectId = 1,
                PageIndex = 1,
                PageSize = 10
            };

            var reports = new List<InspectionReportDTO>
            {
                new InspectionReportDTO
                {
                    Id = 1,
                    ProjectId = 1,
                    ProjectName = "Test Project 1",
                    InspectCode = "IR001",
                    InspectorId = 1,
                    Status = InspectionReportStatus.Draft
                },
                new InspectionReportDTO
                {
                    Id = 2,
                    ProjectId = 1,
                    ProjectName = "Test Project 1",
                    InspectCode = "IR002",
                    InspectorId = 2,
                    Status = InspectionReportStatus.Submitted
                }
            };

            _mockInspectionReportService
                .Setup(s => s.List(It.IsAny<SearchInspectionReportDTO>()))
                .ReturnsAsync(reports);

            // Act
            var result = await _controller.List(searchModel);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.InspectionReportMessage.SEARCH_SUCCESS);
            result.Data.Should().NotBeNull();
            result.Data.Should().HaveCount(2);
            result.Data[0].InspectCode.Should().Be("IR001");
            result.Data[1].InspectCode.Should().Be("IR002");
            
            // Verify that ActionBy was set correctly
            _mockInspectionReportService.Verify(s => s.List(
                It.Is<SearchInspectionReportDTO>(dto => dto.ActionBy == 1)),
                Times.Once);
        }

        [Fact]
        public async Task List_ReturnsEmptyList_WhenNoReportsFound()
        {
            // Arrange
            var searchModel = new SearchInspectionReportDTO { ProjectId = 999 };

            _mockInspectionReportService
                .Setup(s => s.List(It.IsAny<SearchInspectionReportDTO>()))
                .ReturnsAsync(new List<InspectionReportDTO>());

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
            var searchModel = new SearchInspectionReportDTO();

            _mockInspectionReportService
                .Setup(s => s.List(It.IsAny<SearchInspectionReportDTO>()))
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
        public async Task Detail_ReturnsSuccessResponse_WithInspectionReportDetails()
        {
            // Arrange
            int reportId = 1;
            var report = new InspectionReportDTO
            {
                Id = 1,
                ProjectId = 1,
                ProjectName = "Test Project",
                InspectCode = "IR001",
                InspectorId = 1,
                InspectorName = "Inspector 1",
                InspectStartDate = DateTime.UtcNow.AddDays(-1),
                InspectEndDate = DateTime.UtcNow,
                Status = InspectionReportStatus.Draft,
                QualityNote = "Quality note",
                OtherNote = "Other note"
            };

            _mockInspectionReportService
                .Setup(s => s.Detail(reportId, It.IsAny<int>()))
                .ReturnsAsync(report);

            // Act
            var result = await _controller.Detail(reportId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.InspectionReportMessage.GET_DETAIL_SUCCESS);
            result.Data.Should().NotBeNull();
            result.Data.Id.Should().Be(1);
            result.Data.InspectCode.Should().Be("IR001");
            result.Data.ProjectName.Should().Be("Test Project");
            
            // Verify that correct userId was passed
            _mockInspectionReportService.Verify(s => s.Detail(reportId, 1), Times.Once);
        }

        [Fact]
        public async Task Detail_ReturnsFailureResponse_WhenReportNotFound()
        {
            // Arrange
            int reportId = 999;

            _mockInspectionReportService
                .Setup(s => s.Detail(reportId, It.IsAny<int>()))
                .ThrowsAsync(new KeyNotFoundException(Message.InspectionReportMessage.NOT_FOUND));

            // Act
            var result = await _controller.Detail(reportId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(404);
            result.Message.Should().Be(Message.InspectionReportMessage.NOT_FOUND);
        }

        [Fact]
        public async Task Detail_ReturnsFailureResponse_WhenUserNotAuthorized()
        {
            // Arrange
            int reportId = 1;

            _mockInspectionReportService
                .Setup(s => s.Detail(reportId, It.IsAny<int>()))
                .ThrowsAsync(new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED));

            // Act
            var result = await _controller.Detail(reportId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(403);
            result.Message.Should().Be(Message.CommonMessage.NOT_ALLOWED);
        }

        [Fact]
        public async Task GetByProject_ReturnsSuccessResponse_WithInspectionReports()
        {
            // Arrange
            int projectId = 1;
            var reports = new List<InspectionReportDTO>
            {
                new InspectionReportDTO
                {
                    Id = 1,
                    ProjectId = 1,
                    ProjectName = "Test Project",
                    InspectCode = "IR001",
                    Status = InspectionReportStatus.Draft
                },
                new InspectionReportDTO
                {
                    Id = 2,
                    ProjectId = 1,
                    ProjectName = "Test Project",
                    InspectCode = "IR002",
                    Status = InspectionReportStatus.Submitted
                }
            };

            _mockInspectionReportService
                .Setup(s => s.GetByProject(projectId, It.IsAny<int>()))
                .ReturnsAsync(reports);

            // Act
            var result = await _controller.GetByProject(projectId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.InspectionReportMessage.GET_BY_PROJECT_SUCCESS);
            result.Data.Should().NotBeNull();
            result.Data.Should().HaveCount(2);
            result.Data.All(r => r.ProjectId == projectId).Should().BeTrue();
            
            // Verify that correct userId was passed
            _mockInspectionReportService.Verify(s => s.GetByProject(projectId, 1), Times.Once);
        }

        [Fact]
        public async Task GetByProject_ReturnsFailureResponse_WhenProjectNotFound()
        {
            // Arrange
            int projectId = 999;

            _mockInspectionReportService
                .Setup(s => s.GetByProject(projectId, It.IsAny<int>()))
                .ThrowsAsync(new KeyNotFoundException(Message.InspectionReportMessage.PROJECT_NOT_FOUND));

            // Act
            var result = await _controller.GetByProject(projectId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(404);
            result.Message.Should().Be(Message.InspectionReportMessage.PROJECT_NOT_FOUND);
        }

        [Fact]
        public async Task Save_ReturnsSuccessResponse_WhenCreatingReport()
        {
            // Arrange
            var model = new SaveInspectionReportDTO
            {
                Id = 0, // New report
                ConstructionProgressItemId = 1,
                InspectorId = 1,
                InspectStartDate = DateTime.UtcNow.AddDays(-1),
                InspectEndDate = DateTime.UtcNow,
                Location = "Test Location",
                InspectionDecision = InspectionDecision.Pass,
                Status = InspectionReportStatus.Draft,
                QualityNote = "Quality note",
                OtherNote = "Other note"
            };

            var createdReport = new InspectionReportDTO
            {
                Id = 1,
                ConstructionProgressItemId = 1,
                ProgressItemName = "Test Work Item",
                ProjectName = "Test Project",
                InspectCode = "IR001",
                InspectorId = 1,
                InspectorName = "Inspector 1",
                InspectStartDate = model.InspectStartDate,
                InspectEndDate = model.InspectEndDate,
                Status = InspectionReportStatus.Draft,
                QualityNote = "Quality note",
                OtherNote = "Other note"
            };

            _mockInspectionReportService
                .Setup(s => s.Save(It.IsAny<SaveInspectionReportDTO>(), It.IsAny<int>()))
                .ReturnsAsync(createdReport);

            // Act
            var result = await _controller.Save(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.InspectionReportMessage.CREATE_SUCCESS);
            result.Data.Should().NotBeNull();
            result.Data.Id.Should().Be(1);
            
            // Verify that correct userId was passed
            _mockInspectionReportService.Verify(s => s.Save(model, 1), Times.Once);
        }

        [Fact]
        public async Task Save_ReturnsSuccessResponse_WhenUpdatingReport()
        {
            // Arrange
            var model = new SaveInspectionReportDTO
            {
                Id = 1, // Existing report
                ConstructionProgressItemId = 1,
                InspectorId = 1,
                InspectStartDate = DateTime.UtcNow.AddDays(-1),
                InspectEndDate = DateTime.UtcNow,
                Location = "Updated Location",
                InspectionDecision = InspectionDecision.PassWithRemarks,
                Status = InspectionReportStatus.Submitted,
                QualityNote = "Updated quality note",
                OtherNote = "Updated other note"
            };

            var updatedReport = new InspectionReportDTO
            {
                Id = 1,
                ConstructionProgressItemId = 1,
                ProjectName = "Test Project",
                InspectCode = "IR001",
                InspectorId = 1,
                InspectorName = "Inspector 1",
                InspectStartDate = model.InspectStartDate,
                InspectEndDate = model.InspectEndDate,
                Status = InspectionReportStatus.Submitted,
                QualityNote = "Updated quality note",
                OtherNote = "Updated other note"
            };

            _mockInspectionReportService
                .Setup(s => s.Save(It.IsAny<SaveInspectionReportDTO>(), It.IsAny<int>()))
                .ReturnsAsync(updatedReport);

            // Act
            var result = await _controller.Save(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.InspectionReportMessage.UPDATE_SUCCESS);
            result.Data.Should().NotBeNull();
            result.Data.Id.Should().Be(1);
            result.Data.Status.Should().Be(InspectionReportStatus.Submitted);
            
            // Verify that correct userId was passed
            _mockInspectionReportService.Verify(s => s.Save(model, 1), Times.Once);
        }

        [Fact]
        public async Task Save_ReturnsFailureResponse_WhenProgressItemNotFound()
        {
            // Arrange
            var model = new SaveInspectionReportDTO
            {
                ConstructionProgressItemId = 999,
                InspectorId = 1
            };

            _mockInspectionReportService
                .Setup(s => s.Save(It.IsAny<SaveInspectionReportDTO>(), It.IsAny<int>()))
                .ThrowsAsync(new KeyNotFoundException(Message.InspectionReportMessage.NOT_FOUND));

            // Act
            var result = await _controller.Save(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(404);
            result.Message.Should().Be(Message.InspectionReportMessage.NOT_FOUND);
        }

        [Fact]
        public async Task Save_ReturnsFailureResponse_WhenUserNotAuthorized()
        {
            // Arrange
            var model = new SaveInspectionReportDTO
            {
                ConstructionProgressItemId = 1,
                InspectorId = 1
            };

            _mockInspectionReportService
                .Setup(s => s.Save(It.IsAny<SaveInspectionReportDTO>(), It.IsAny<int>()))
                .ThrowsAsync(new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED));

            // Act
            var result = await _controller.Save(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(403);
            result.Message.Should().Be(Message.CommonMessage.NOT_ALLOWED);
        }

        [Fact]
        public async Task Approve_ReturnsSuccessResponse_WhenReportApproved()
        {
            // Arrange
            int reportId = 1;
            var approvedReport = new InspectionReportDTO
            {
                Id = 1,
                Status = InspectionReportStatus.Approved
            };

            _mockInspectionReportService
                .Setup(s => s.Save(It.IsAny<SaveInspectionReportDTO>(), It.IsAny<int>()))
                .ReturnsAsync(approvedReport);

            // Act
            var result = await _controller.Approve(reportId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.InspectionReportMessage.APPROVE_SUCCESS);
            result.Data.Should().NotBeNull();
            result.Data.Status.Should().Be(InspectionReportStatus.Approved);
            
            // Verify that correct model was passed with Approved status
            _mockInspectionReportService.Verify(s => s.Save(
                It.Is<SaveInspectionReportDTO>(dto => 
                    dto.Id == reportId && 
                    dto.Status == InspectionReportStatus.Approved), 
                1), 
                Times.Once);
        }

        [Fact]
        public async Task Reject_ReturnsSuccessResponse_WhenReportRejected()
        {
            // Arrange
            int reportId = 1;
            var rejectedReport = new InspectionReportDTO
            {
                Id = 1,
                Status = InspectionReportStatus.Rejected
            };

            _mockInspectionReportService
                .Setup(s => s.Save(It.IsAny<SaveInspectionReportDTO>(), It.IsAny<int>()))
                .ReturnsAsync(rejectedReport);

            // Act
            var result = await _controller.Reject(reportId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.InspectionReportMessage.REJECT_SUCCESS);
            result.Data.Should().NotBeNull();
            result.Data.Status.Should().Be(InspectionReportStatus.Rejected);
            
            // Verify that correct model was passed with Rejected status
            _mockInspectionReportService.Verify(s => s.Save(
                It.Is<SaveInspectionReportDTO>(dto => 
                    dto.Id == reportId && 
                    dto.Status == InspectionReportStatus.Rejected), 
                1), 
                Times.Once);
        }

        [Fact]
        public async Task Delete_ReturnsSuccessResponse_WhenReportDeleted()
        {
            // Arrange
            int reportId = 1;

            _mockInspectionReportService
                .Setup(s => s.Delete(reportId, It.IsAny<int>()))
                .ReturnsAsync(reportId);

            // Act
            var result = await _controller.Delete(reportId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.InspectionReportMessage.DELETE_SUCCESS);
            result.Data.Should().Be(reportId);
            
            // Verify that correct userId was passed
            _mockInspectionReportService.Verify(s => s.Delete(reportId, 1), Times.Once);
        }

        [Fact]
        public async Task Delete_ReturnsFailureResponse_WhenReportNotFound()
        {
            // Arrange
            int reportId = 999;

            _mockInspectionReportService
                .Setup(s => s.Delete(reportId, It.IsAny<int>()))
                .ThrowsAsync(new KeyNotFoundException(Message.InspectionReportMessage.NOT_FOUND));

            // Act
            var result = await _controller.Delete(reportId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(404);
            result.Message.Should().Be(Message.InspectionReportMessage.NOT_FOUND);
        }

        [Fact]
        public async Task Delete_ReturnsFailureResponse_WhenUserNotAuthorized()
        {
            // Arrange
            int reportId = 1;

            _mockInspectionReportService
                .Setup(s => s.Delete(reportId, It.IsAny<int>()))
                .ThrowsAsync(new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED));

            // Act
            var result = await _controller.Delete(reportId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(403);
            result.Message.Should().Be(Message.CommonMessage.NOT_ALLOWED);
        }
    }
} 