using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Sep490_Backend.Controllers;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.SiteSurvey;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Services.DataService;
using Sep490_Backend.Services.SiteSurveyService;
using System.Security.Claims;
using System.Text.Json;

namespace UnitTest_SEP490.Controllers
{
    public class SiteSurveyControllerTests
    {
        private readonly Mock<ISiteSurveyService> _mockSiteSurveyService;
        private readonly Mock<IDataService> _mockDataService;
        private readonly SiteSurveyController _controller;

        public SiteSurveyControllerTests()
        {
            _mockSiteSurveyService = new Mock<ISiteSurveyService>();
            _mockDataService = new Mock<IDataService>();
            _controller = new SiteSurveyController(_mockSiteSurveyService.Object, _mockDataService.Object);

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
                HttpContext = new DefaultHttpContext
                {
                    User = claimsPrincipal
                }
            };
        }

        [Fact]
        public async Task DeleteSiteSurvey_ReturnsSuccessResponse_WhenSurveyDeleted()
        {
            // Arrange
            int surveyId = 1;

            _mockSiteSurveyService
                .Setup(s => s.DeleteSiteSurvey(surveyId, It.IsAny<int>()))
                .ReturnsAsync(surveyId);

            // Act
            var result = await _controller.DeleteSiteSurvey(surveyId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.SiteSurveyMessage.DELETE_SUCCESS);
            result.Data.Should().Be(surveyId);
        }

        [Fact]
        public async Task DeleteSiteSurvey_ReturnsFailureResponse_WhenSurveyNotFound()
        {
            // Arrange
            int surveyId = 999;

            _mockSiteSurveyService
                .Setup(s => s.DeleteSiteSurvey(surveyId, It.IsAny<int>()))
                .ThrowsAsync(new KeyNotFoundException(Message.CommonMessage.NOT_FOUND));

            // Act
            var result = await _controller.DeleteSiteSurvey(surveyId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(404);
            result.Message.Should().Be(Message.CommonMessage.NOT_FOUND);
        }

        [Fact]
        public async Task DeleteSiteSurvey_ReturnsFailureResponse_WhenUnauthorized()
        {
            // Arrange
            int surveyId = 1;

            _mockSiteSurveyService
                .Setup(s => s.DeleteSiteSurvey(surveyId, It.IsAny<int>()))
                .ThrowsAsync(new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED));

            // Act
            var result = await _controller.DeleteSiteSurvey(surveyId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(403);
            result.Message.Should().Be(Message.CommonMessage.NOT_ALLOWED);
        }

        [Fact]
        public async Task DeleteSiteSurvey_ReturnsFailureResponse_WhenServiceThrowsException()
        {
            // Arrange
            int surveyId = 1;

            _mockSiteSurveyService
                .Setup(s => s.DeleteSiteSurvey(surveyId, It.IsAny<int>()))
                .ThrowsAsync(new Exception(Message.CommonMessage.ERROR_HAPPENED));

            // Act
            var result = await _controller.DeleteSiteSurvey(surveyId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(500);
            result.Message.Should().Be(Message.CommonMessage.ERROR_HAPPENED);
        }

        [Fact]
        public async Task SaveSiteSurvey_ReturnsSuccessResponse_WithSavedSurvey()
        {
            // Arrange
            var model = new SaveSiteSurveyDTO
            {
                Id = 0,
                ProjectId = 1,
                SiteSurveyName = "Test Site Survey",
                ConstructionRequirements = "Test construction requirements",
                EquipmentRequirements = "Test equipment requirements",
                HumanResourceCapacity = "Test HR capacity",
                RiskAssessment = "Test risk assessment",
                BiddingDecision = 1,
                ProfitAssessment = "Test profit assessment",
                BidWinProb = 0.75,
                EstimatedExpenses = 100000,
                EstimatedProfits = 25000,
                TenderPackagePrice = 150000,
                TotalBidPrice = 125000,
                DiscountRate = 0.05,
                ProjectCost = 90000,
                FinalProfit = 35000,
                Status = 1,
                Comments = "Test comments",
                SurveyDate = DateTime.Now
            };

            var savedSurvey = new SiteSurvey
            {
                Id = 1,
                ProjectId = 1,
                SiteSurveyName = "Test Site Survey",
                ConstructionRequirements = "Test construction requirements",
                EquipmentRequirements = "Test equipment requirements",
                HumanResourceCapacity = "Test HR capacity",
                RiskAssessment = "Test risk assessment",
                BiddingDecision = 1,
                ProfitAssessment = "Test profit assessment",
                BidWinProb = 0.75,
                EstimatedExpenses = 100000,
                EstimatedProfits = 25000,
                TenderPackagePrice = 150000,
                TotalBidPrice = 125000,
                DiscountRate = 0.05,
                ProjectCost = 90000,
                FinalProfit = 35000,
                Status = 1,
                Comments = "Test comments",
                SurveyDate = model.SurveyDate,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Creator = 1,
                Updater = 1,
                Deleted = false
            };

            _mockSiteSurveyService
                .Setup(s => s.SaveSiteSurvey(It.IsAny<SaveSiteSurveyDTO>(), It.IsAny<int>()))
                .ReturnsAsync(savedSurvey);

            // Act
            var result = await _controller.SaveSiteSurvey(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.SiteSurveyMessage.SAVE_SUCCESS);
            result.Data.Should().NotBeNull();
            result.Data.Id.Should().Be(1);
            result.Data.SiteSurveyName.Should().Be("Test Site Survey");
            result.Data.ProjectId.Should().Be(1);
        }

        [Fact]
        public async Task SaveSiteSurvey_ReturnsFailureResponse_WhenValidationFails()
        {
            // Arrange
            var model = new SaveSiteSurveyDTO 
            { 
                ProjectId = 1,
                SiteSurveyName = "" // Required field is empty
            };

            var errors = new List<ResponseError>
            {
                new ResponseError { Field = "SiteSurveyName", Message = "Site survey name is required for documentation" }
            };

            _mockSiteSurveyService
                .Setup(s => s.SaveSiteSurvey(It.IsAny<SaveSiteSurveyDTO>(), It.IsAny<int>()))
                .ThrowsAsync(new Sep490_Backend.Controllers.ValidationException(errors));

            // Act
            var result = await _controller.SaveSiteSurvey(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(400);
            result.Errors.Should().NotBeEmpty();
            result.Errors.Should().HaveCount(1);
            result.Errors[0].Field.Should().Be("SiteSurveyName");
        }

        [Fact]
        public async Task SaveSiteSurvey_ReturnsFailureResponse_WhenUnauthorized()
        {
            // Arrange
            var model = new SaveSiteSurveyDTO
            {
                ProjectId = 1,
                SiteSurveyName = "Test Site Survey"
            };

            _mockSiteSurveyService
                .Setup(s => s.SaveSiteSurvey(It.IsAny<SaveSiteSurveyDTO>(), It.IsAny<int>()))
                .ThrowsAsync(new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED));

            // Act
            var result = await _controller.SaveSiteSurvey(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(403);
            result.Message.Should().Be(Message.CommonMessage.NOT_ALLOWED);
        }

        [Fact]
        public async Task SaveSiteSurvey_ReturnsFailureResponse_WhenServiceThrowsException()
        {
            // Arrange
            var model = new SaveSiteSurveyDTO
            {
                ProjectId = 1,
                SiteSurveyName = "Test Site Survey"
            };

            _mockSiteSurveyService
                .Setup(s => s.SaveSiteSurvey(It.IsAny<SaveSiteSurveyDTO>(), It.IsAny<int>()))
                .ThrowsAsync(new Exception(Message.CommonMessage.ERROR_HAPPENED));

            // Act
            var result = await _controller.SaveSiteSurvey(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(500);
            result.Message.Should().Be(Message.CommonMessage.ERROR_HAPPENED);
        }

        [Fact]
        public async Task GetSiteSurveyDetail_ReturnsSuccessResponse_WithSurveyDetail()
        {
            // Arrange
            int projectId = 1;

            var survey = new SiteSurvey
            {
                Id = 1,
                ProjectId = projectId,
                SiteSurveyName = "Test Site Survey",
                ConstructionRequirements = "Test construction requirements",
                EquipmentRequirements = "Test equipment requirements",
                HumanResourceCapacity = "Test HR capacity",
                RiskAssessment = "Test risk assessment",
                BiddingDecision = 1,
                ProfitAssessment = "Test profit assessment",
                BidWinProb = 0.75,
                EstimatedExpenses = 100000,
                EstimatedProfits = 25000,
                TenderPackagePrice = 150000,
                TotalBidPrice = 125000,
                DiscountRate = 0.05,
                ProjectCost = 90000,
                FinalProfit = 35000,
                Status = 1,
                Comments = "Test comments",
                SurveyDate = DateTime.Now,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Creator = 1,
                Updater = 1,
                Deleted = false
            };

            _mockSiteSurveyService
                .Setup(s => s.GetSiteSurveyDetail(projectId, It.IsAny<int>()))
                .ReturnsAsync(survey);

            // Act
            var result = await _controller.GetSiteSurveyDetail(projectId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.SiteSurveyMessage.GET_DETAIL_SUCCESS);
            result.Data.Should().NotBeNull();
            result.Data.Id.Should().Be(1);
            result.Data.ProjectId.Should().Be(projectId);
            result.Data.SiteSurveyName.Should().Be("Test Site Survey");
        }

        [Fact]
        public async Task GetSiteSurveyDetail_ReturnsFailureResponse_WhenSurveyNotFound()
        {
            // Arrange
            int projectId = 999;

            _mockSiteSurveyService
                .Setup(s => s.GetSiteSurveyDetail(projectId, It.IsAny<int>()))
                .ThrowsAsync(new KeyNotFoundException(Message.CommonMessage.NOT_FOUND));

            // Act
            var result = await _controller.GetSiteSurveyDetail(projectId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(404);
            result.Message.Should().Be(Message.CommonMessage.NOT_FOUND);
        }

        [Fact]
        public async Task GetSiteSurveyDetail_ReturnsFailureResponse_WhenUnauthorized()
        {
            // Arrange
            int projectId = 1;

            _mockSiteSurveyService
                .Setup(s => s.GetSiteSurveyDetail(projectId, It.IsAny<int>()))
                .ThrowsAsync(new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED));

            // Act
            var result = await _controller.GetSiteSurveyDetail(projectId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(403);
            result.Message.Should().Be(Message.CommonMessage.NOT_ALLOWED);
        }

        [Fact]
        public async Task GetSiteSurveyDetail_ReturnsFailureResponse_WhenServiceThrowsException()
        {
            // Arrange
            int projectId = 1;

            _mockSiteSurveyService
                .Setup(s => s.GetSiteSurveyDetail(projectId, It.IsAny<int>()))
                .ThrowsAsync(new Exception(Message.CommonMessage.ERROR_HAPPENED));

            // Act
            var result = await _controller.GetSiteSurveyDetail(projectId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(500);
            result.Message.Should().Be(Message.CommonMessage.ERROR_HAPPENED);
        }
    }
} 