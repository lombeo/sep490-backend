using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Sep490_Backend.Controllers;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.Material;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Services.CacheService;
using Sep490_Backend.Services.DataService;
using Sep490_Backend.Services.MaterialService;
using System.Security.Claims;

namespace UnitTest_SEP490.Controllers
{
    public class MaterialControllerTests
    {
        private readonly Mock<IMaterialService> _mockMaterialService;
        private readonly Mock<IDataService> _mockDataService;
        private readonly Mock<ILogger<MaterialController>> _mockLogger;
        private readonly Mock<ICacheService> _mockCacheService;
        private readonly MaterialController _controller;

        public MaterialControllerTests()
        {
            _mockMaterialService = new Mock<IMaterialService>();
            _mockDataService = new Mock<IDataService>();
            _mockLogger = new Mock<ILogger<MaterialController>>();
            _mockCacheService = new Mock<ICacheService>();
            _controller = new MaterialController(_mockMaterialService.Object, _mockDataService.Object, _mockLogger.Object, _mockCacheService.Object);

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
        public async Task Search_ReturnsSuccessResponse_WithMaterials()
        {
            // Arrange
            var searchModel = new MaterialSearchDTO
            {
                Keyword = "Test",
                PageIndex = 1,
                PageSize = 10
            };

            var materials = new List<Material>
            {
                new Material
                {
                    Id = 1,
                    MaterialCode = "M001",
                    MaterialName = "Test Material 1",
                    Unit = "Piece",
                    Inventory = 10
                },
                new Material
                {
                    Id = 2,
                    MaterialCode = "M002",
                    MaterialName = "Test Material 2",
                    Unit = "Kg",
                    Inventory = 20
                }
            };

            _mockDataService
                .Setup(s => s.ListMaterial(It.IsAny<MaterialSearchDTO>()))
                .ReturnsAsync(materials);

            // Act
            var result = await _controller.Search(searchModel);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.CommonMessage.ACTION_SUCCESS);
            result.Data.Should().NotBeNull();
            result.Data.Should().HaveCount(2);
            result.Data[0].MaterialCode.Should().Be("M001");
            result.Data[1].MaterialCode.Should().Be("M002");
            result.Meta.Should().NotBeNull();
            result.Meta.Total.Should().Be(searchModel.Total);
            result.Meta.PageSize.Should().Be(searchModel.PageSize);
            result.Meta.Index.Should().Be(searchModel.PageIndex);
            
            // Verify that ActionBy was set correctly
            _mockDataService.Verify(s => s.ListMaterial(
                It.Is<MaterialSearchDTO>(dto => dto.ActionBy == 1)), 
                Times.Once);
        }

        [Fact]
        public async Task Search_ReturnsEmptyList_WhenNoMaterialsFound()
        {
            // Arrange
            var searchModel = new MaterialSearchDTO { Keyword = "NonExistent" };

            _mockDataService
                .Setup(s => s.ListMaterial(It.IsAny<MaterialSearchDTO>()))
                .ReturnsAsync(new List<Material>());

            // Act
            var result = await _controller.Search(searchModel);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Data.Should().BeEmpty();
        }

        [Fact]
        public async Task Search_ReturnsFailureResponse_WhenServiceThrowsException()
        {
            // Arrange
            var searchModel = new MaterialSearchDTO();

            _mockDataService
                .Setup(s => s.ListMaterial(It.IsAny<MaterialSearchDTO>()))
                .ThrowsAsync(new Exception(Message.CommonMessage.ERROR_HAPPENED));

            // Act
            var result = await _controller.Search(searchModel);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(500);
            result.Message.Should().Be(Message.CommonMessage.ERROR_HAPPENED);
        }

        [Fact]
        public async Task GetMaterialById_ReturnsSuccessResponse_WithMaterialDetails()
        {
            // Arrange
            int materialId = 1;
            var material = new MaterialDetailDTO
            {
                Id = 1,
                MaterialCode = "M001",
                MaterialName = "Test Material",
                Unit = "Piece",
                Branch = "Test Branch",
                MadeIn = "Test Country",
                ChassisNumber = "TCHS001",
                WholesalePrice = 100.50m,
                RetailPrice = 150.00m,
                Inventory = 10,
                Attachment = "test-attachment.jpg",
                Description = "Test Description"
            };

            _mockMaterialService
                .Setup(s => s.GetMaterialById(materialId, It.IsAny<int>()))
                .ReturnsAsync(material);

            // Act
            var result = await _controller.GetMaterialById(materialId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.MaterialMessage.GET_DETAIL_SUCCESS);
            result.Data.Should().NotBeNull();
            result.Data.Id.Should().Be(1);
            result.Data.MaterialCode.Should().Be("M001");
            result.Data.MaterialName.Should().Be("Test Material");
            
            // Verify that correct userId was passed
            _mockMaterialService.Verify(s => s.GetMaterialById(materialId, 1), Times.Once);

            // Verify that logging occurred
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Getting material details")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetMaterialById_ReturnsFailureResponse_WhenMaterialNotFound()
        {
            // Arrange
            int materialId = 999;

            _mockMaterialService
                .Setup(s => s.GetMaterialById(materialId, It.IsAny<int>()))
                .ThrowsAsync(new KeyNotFoundException(Message.MaterialMessage.NOT_FOUND));

            // Act
            var result = await _controller.GetMaterialById(materialId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(404);
            result.Message.Should().Be(Message.MaterialMessage.NOT_FOUND);
        }

        [Fact]
        public async Task DeleteMaterial_ReturnsSuccessResponse_WhenMaterialDeleted()
        {
            // Arrange
            int materialId = 1;

            _mockMaterialService
                .Setup(s => s.DeleteMaterial(materialId, It.IsAny<int>()))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.DeleteMaterial(materialId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.MaterialMessage.DELETE_SUCCESS);
            result.Data.Should().BeTrue();
            
            // Verify that correct userId was passed
            _mockMaterialService.Verify(s => s.DeleteMaterial(materialId, 1), Times.Once);

            // Verify that logging occurred
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Deleting material")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task DeleteMaterial_ReturnsFailureResponse_WhenMaterialNotFound()
        {
            // Arrange
            int materialId = 999;

            _mockMaterialService
                .Setup(s => s.DeleteMaterial(materialId, It.IsAny<int>()))
                .ThrowsAsync(new KeyNotFoundException(Message.MaterialMessage.NOT_FOUND));

            // Act
            var result = await _controller.DeleteMaterial(materialId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(404);
            result.Message.Should().Be(Message.MaterialMessage.NOT_FOUND);
        }

        [Fact]
        public async Task DeleteMaterial_ReturnsFailureResponse_WhenMaterialInUse()
        {
            // Arrange
            int materialId = 1;

            _mockMaterialService
                .Setup(s => s.DeleteMaterial(materialId, It.IsAny<int>()))
                .ThrowsAsync(new InvalidOperationException(Message.MaterialMessage.MATERIAL_IN_USE));

            // Act
            var result = await _controller.DeleteMaterial(materialId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(400);
            result.Message.Should().Be(Message.MaterialMessage.MATERIAL_IN_USE);
        }

        [Fact]
        public async Task DeleteMaterial_ReturnsFailureResponse_WhenUserNotAuthorized()
        {
            // Arrange
            int materialId = 1;

            _mockMaterialService
                .Setup(s => s.DeleteMaterial(materialId, It.IsAny<int>()))
                .ThrowsAsync(new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED));

            // Act
            var result = await _controller.DeleteMaterial(materialId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(403);
            result.Message.Should().Be(Message.CommonMessage.NOT_ALLOWED);
        }

        [Fact]
        public async Task SaveMaterial_ReturnsSuccessResponse_WithSavedMaterial()
        {
            // Arrange
            var model = new MaterialSaveDTO
            {
                MaterialCode = "M003",
                MaterialName = "New Material",
                Unit = "Box",
                Branch = "New Branch",
                MadeIn = "New Country",
                WholesalePrice = 200.50m,
                RetailPrice = 250.00m,
                Inventory = 30
            };

            var savedMaterial = new Material
            {
                Id = 3,
                MaterialCode = "M003",
                MaterialName = "New Material",
                Unit = "Box",
                Branch = "New Branch",
                MadeIn = "New Country",
                WholesalePrice = 200.50m,
                RetailPrice = 250.00m,
                Inventory = 30
            };

            _mockMaterialService
                .Setup(s => s.SaveMaterial(It.IsAny<MaterialSaveDTO>(), It.IsAny<int>()))
                .ReturnsAsync(savedMaterial);

            // Act
            var result = await _controller.SaveMaterial(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.MaterialMessage.SAVE_SUCCESS);
            result.Data.Should().NotBeNull();
            result.Data.Id.Should().Be(3);
            result.Data.MaterialCode.Should().Be("M003");
            
            // Verify that correct userId was passed
            _mockMaterialService.Verify(s => s.SaveMaterial(model, 1), Times.Once);

            // Verify that logging occurred
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Saving material")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SaveMaterial_ReturnsFailureResponse_WhenValidationFails()
        {
            // Arrange
            var model = new MaterialSaveDTO
            {
                MaterialCode = "M001", // Duplicate code
                MaterialName = "" // Missing required field
            };

            var errors = new List<ResponseError>
            {
                new ResponseError { Field = "materialCode", Message = Message.MaterialMessage.CODE_EXISTS },
                new ResponseError { Field = "materialName", Message = Message.MaterialMessage.NAME_REQUIRED }
            };

            _mockMaterialService
                .Setup(s => s.SaveMaterial(It.IsAny<MaterialSaveDTO>(), It.IsAny<int>()))
                .ThrowsAsync(new ValidationException(errors));

            // Act
            var result = await _controller.SaveMaterial(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(400);
            result.Errors.Should().NotBeNull();
            result.Errors.Should().HaveCount(2);
            result.Errors[0].Field.Should().Be("materialCode");
            result.Errors[0].Message.Should().Be(Message.MaterialMessage.CODE_EXISTS);
            result.Errors[1].Field.Should().Be("materialName");
            result.Errors[1].Message.Should().Be(Message.MaterialMessage.NAME_REQUIRED);
        }

        [Fact]
        public async Task SaveMaterial_ReturnsFailureResponse_WhenUserNotAuthorized()
        {
            // Arrange
            var model = new MaterialSaveDTO
            {
                MaterialCode = "M003",
                MaterialName = "New Material"
            };

            _mockMaterialService
                .Setup(s => s.SaveMaterial(It.IsAny<MaterialSaveDTO>(), It.IsAny<int>()))
                .ThrowsAsync(new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED));

            // Act
            var result = await _controller.SaveMaterial(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(403);
            result.Message.Should().Be(Message.CommonMessage.NOT_ALLOWED);
        }
    }
} 