using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Sep490_Backend.Controllers;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.Customer;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Services.CustomerService;
using Sep490_Backend.Services.DataService;
using System.Security.Claims;

namespace UnitTest_SEP490.Controllers
{
    public class CustomerControllerTests
    {
        private readonly Mock<ICustomerService> _mockCustomerService;
        private readonly Mock<IDataService> _mockDataService;
        private readonly CustomerController _controller;

        public CustomerControllerTests()
        {
            _mockCustomerService = new Mock<ICustomerService>();
            _mockDataService = new Mock<IDataService>();
            _controller = new CustomerController(_mockCustomerService.Object, _mockDataService.Object);

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
        public async Task GetListCustomer_ReturnsSuccessResponse_WithCustomers()
        {
            // Arrange
            var searchModel = new CustomerSearchDTO
            {
                Search = "Test",
                PageIndex = 1,
                PageSize = 10
            };

            var customers = new List<Customer>
            {
                new Customer
                {
                    Id = 1,
                    CustomerCode = "C001",
                    CustomerName = "Test Customer 1",
                    TaxCode = "TAX001",
                    Phone = "1234567890"
                },
                new Customer
                {
                    Id = 2,
                    CustomerCode = "C002",
                    CustomerName = "Test Customer 2",
                    TaxCode = "TAX002",
                    Phone = "0987654321"
                }
            };

            _mockDataService
                .Setup(s => s.ListCustomer(It.IsAny<CustomerSearchDTO>()))
                .ReturnsAsync(customers);

            // Act
            var result = await _controller.GetListCustomer(searchModel);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.CustomerMessage.SEARCH_SUCCESS);
            result.Data.Should().NotBeNull();
            result.Data.Should().HaveCount(2);
            result.Data[0].CustomerCode.Should().Be("C001");
            result.Data[1].CustomerCode.Should().Be("C002");
            result.Meta.Should().NotBeNull();
            result.Meta.Total.Should().Be(searchModel.Total);
            result.Meta.PageSize.Should().Be(searchModel.PageSize);
            result.Meta.Index.Should().Be(searchModel.PageIndex);
            
            // Verify that ActionBy was set correctly
            _mockDataService.Verify(s => s.ListCustomer(
                It.Is<CustomerSearchDTO>(dto => dto.ActionBy == 1)), 
                Times.Once);
        }

        [Fact]
        public async Task GetListCustomer_ReturnsEmptyList_WhenNoCustomersFound()
        {
            // Arrange
            var searchModel = new CustomerSearchDTO { Search = "NonExistent" };

            _mockDataService
                .Setup(s => s.ListCustomer(It.IsAny<CustomerSearchDTO>()))
                .ReturnsAsync(new List<Customer>());

            // Act
            var result = await _controller.GetListCustomer(searchModel);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Data.Should().BeEmpty();
        }

        [Fact]
        public async Task GetListCustomer_ReturnsFailureResponse_WhenServiceThrowsException()
        {
            // Arrange
            var searchModel = new CustomerSearchDTO();

            _mockDataService
                .Setup(s => s.ListCustomer(It.IsAny<CustomerSearchDTO>()))
                .ThrowsAsync(new Exception(Message.CommonMessage.ERROR_HAPPENED));

            // Act
            var result = await _controller.GetListCustomer(searchModel);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(500);
            result.Message.Should().Be(Message.CommonMessage.ERROR_HAPPENED);
        }

        [Fact]
        public async Task DetailCustomer_ReturnsSuccessResponse_WithCustomerDetails()
        {
            // Arrange
            int customerId = 1;
            var customer = new Customer
            {
                Id = 1,
                CustomerCode = "C001",
                CustomerName = "Test Customer",
                TaxCode = "TAX001",
                Phone = "1234567890",
                Email = "test@example.com",
                Address = "Test Address",
                DirectorName = "Test Director",
                Description = "Test Description",
                BankAccount = "12345678",
                BankName = "Test Bank"
            };

            _mockCustomerService
                .Setup(s => s.GetDetailCustomer(customerId, It.IsAny<int>()))
                .ReturnsAsync(customer);

            // Act
            var result = await _controller.DetailCustomer(customerId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.CustomerMessage.SEARCH_SUCCESS);
            result.Data.Should().NotBeNull();
            result.Data.Id.Should().Be(1);
            result.Data.CustomerCode.Should().Be("C001");
            result.Data.CustomerName.Should().Be("Test Customer");
            result.Data.Email.Should().Be("test@example.com");
            
            // Verify that correct userId was passed
            _mockCustomerService.Verify(s => s.GetDetailCustomer(customerId, 1), Times.Once);
        }

        [Fact]
        public async Task DetailCustomer_ReturnsFailureResponse_WhenCustomerNotFound()
        {
            // Arrange
            int customerId = 999;

            _mockCustomerService
                .Setup(s => s.GetDetailCustomer(customerId, It.IsAny<int>()))
                .ThrowsAsync(new KeyNotFoundException(Message.CustomerMessage.CUSTOMER_NOT_FOUND));

            // Act
            var result = await _controller.DetailCustomer(customerId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(404);
            result.Message.Should().Be(Message.CustomerMessage.CUSTOMER_NOT_FOUND);
        }

        [Fact]
        public async Task DetailCustomer_ReturnsFailureResponse_WhenUserNotAuthorized()
        {
            // Arrange
            int customerId = 1;

            _mockCustomerService
                .Setup(s => s.GetDetailCustomer(customerId, It.IsAny<int>()))
                .ThrowsAsync(new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED));

            // Act
            var result = await _controller.DetailCustomer(customerId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(403);
            result.Message.Should().Be(Message.CommonMessage.NOT_ALLOWED);
        }

        [Fact]
        public async Task CreateCustomer_ReturnsSuccessResponse_WithCreatedCustomer()
        {
            // Arrange
            var model = new CustomerCreateDTO
            {
                CustomerCode = "C003",
                CustomerName = "New Customer",
                TaxCode = "TAX003",
                Phone = "1231231234",
                Email = "new@example.com",
                Address = "New Address",
                DirectorName = "New Director",
                Description = "New Description",
                BankAccount = "87654321",
                BankName = "New Bank"
            };

            var createdCustomer = new Customer
            {
                Id = 3,
                CustomerCode = "C003",
                CustomerName = "New Customer",
                TaxCode = "TAX003",
                Phone = "1231231234",
                Email = "new@example.com",
                Address = "New Address",
                DirectorName = "New Director",
                Description = "New Description",
                BankAccount = "87654321",
                BankName = "New Bank"
            };

            _mockCustomerService
                .Setup(s => s.CreateCustomer(It.IsAny<CustomerCreateDTO>(), It.IsAny<int>()))
                .ReturnsAsync(createdCustomer);

            // Act
            var result = await _controller.CreateCustomer(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.CustomerMessage.CREATE_CUSTOMER_SUCCESS);
            result.Data.Should().NotBeNull();
            result.Data.Id.Should().Be(3);
            result.Data.CustomerCode.Should().Be("C003");
            
            // Verify that correct userId was passed
            _mockCustomerService.Verify(s => s.CreateCustomer(model, 1), Times.Once);
        }

        [Fact]
        public async Task CreateCustomer_ReturnsFailureResponse_WhenValidationFails()
        {
            // Arrange
            var model = new CustomerCreateDTO
            {
                CustomerCode = "C001", // Duplicate code
                TaxCode = "TAX001"  // Duplicate tax code
            };

            var errors = new List<ResponseError>
            {
                new ResponseError { Field = "customerCode", Message = Message.CustomerMessage.CUSTOMER_CODE_DUPLICATE },
                new ResponseError { Field = "taxCode", Message = Message.CustomerMessage.TAX_CODE_DUPLICATE }
            };

            _mockCustomerService
                .Setup(s => s.CreateCustomer(It.IsAny<CustomerCreateDTO>(), It.IsAny<int>()))
                .ThrowsAsync(new ValidationException(errors));

            // Act
            var result = await _controller.CreateCustomer(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(400);
            result.Errors.Should().NotBeNull();
            result.Errors.Should().HaveCount(2);
            result.Errors[0].Field.Should().Be("customerCode");
            result.Errors[0].Message.Should().Be(Message.CustomerMessage.CUSTOMER_CODE_DUPLICATE);
        }

        [Fact]
        public async Task CreateCustomer_ReturnsFailureResponse_WhenUserNotAuthorized()
        {
            // Arrange
            var model = new CustomerCreateDTO
            {
                CustomerCode = "C003",
                TaxCode = "TAX003"
            };

            _mockCustomerService
                .Setup(s => s.CreateCustomer(It.IsAny<CustomerCreateDTO>(), It.IsAny<int>()))
                .ThrowsAsync(new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED));

            // Act
            var result = await _controller.CreateCustomer(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(403);
            result.Message.Should().Be(Message.CommonMessage.NOT_ALLOWED);
        }

        [Fact]
        public async Task UpdateCustomer_ReturnsSuccessResponse_WithUpdatedCustomer()
        {
            // Arrange
            var model = new CustomerUpdateDTO
            {
                Id = 1,
                CustomerCode = "C001",
                CustomerName = "Updated Customer",
                TaxCode = "TAX001",
                Phone = "9999999999",
                Email = "updated@example.com",
                Address = "Updated Address",
                DirectorName = "Updated Director",
                Description = "Updated Description",
                BankAccount = "11111111",
                BankName = "Updated Bank"
            };

            var updatedCustomer = new Customer
            {
                Id = 1,
                CustomerCode = "C001",
                CustomerName = "Updated Customer",
                TaxCode = "TAX001",
                Phone = "9999999999",
                Email = "updated@example.com",
                Address = "Updated Address",
                DirectorName = "Updated Director",
                Description = "Updated Description",
                BankAccount = "11111111",
                BankName = "Updated Bank"
            };

            _mockCustomerService
                .Setup(s => s.UpdateCustomer(It.IsAny<CustomerUpdateDTO>(), It.IsAny<int>()))
                .ReturnsAsync(updatedCustomer);

            // Act
            var result = await _controller.UpdateCustomer(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.CustomerMessage.UPDATE_CUSTOMER_SUCCESS);
            result.Data.Should().NotBeNull();
            result.Data.Id.Should().Be(1);
            result.Data.CustomerName.Should().Be("Updated Customer");
            
            // Verify that correct userId was passed
            _mockCustomerService.Verify(s => s.UpdateCustomer(model, 1), Times.Once);
        }

        [Fact]
        public async Task UpdateCustomer_ReturnsFailureResponse_WhenCustomerNotFound()
        {
            // Arrange
            var model = new CustomerUpdateDTO
            {
                Id = 999,
                CustomerCode = "C999",
                TaxCode = "TAX999"
            };

            _mockCustomerService
                .Setup(s => s.UpdateCustomer(It.IsAny<CustomerUpdateDTO>(), It.IsAny<int>()))
                .ThrowsAsync(new KeyNotFoundException(Message.CustomerMessage.CUSTOMER_NOT_FOUND));

            // Act
            var result = await _controller.UpdateCustomer(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(404);
            result.Message.Should().Be(Message.CustomerMessage.CUSTOMER_NOT_FOUND);
        }

        [Fact]
        public async Task UpdateCustomer_ReturnsFailureResponse_WhenValidationFails()
        {
            // Arrange
            var model = new CustomerUpdateDTO
            {
                Id = 1,
                CustomerCode = "C002", // Code of another customer
                TaxCode = "TAX002" // Tax code of another customer
            };

            var errors = new List<ResponseError>
            {
                new ResponseError { Field = "customerCode", Message = Message.CustomerMessage.CUSTOMER_CODE_DUPLICATE },
                new ResponseError { Field = "taxCode", Message = Message.CustomerMessage.TAX_CODE_DUPLICATE }
            };

            _mockCustomerService
                .Setup(s => s.UpdateCustomer(It.IsAny<CustomerUpdateDTO>(), It.IsAny<int>()))
                .ThrowsAsync(new ValidationException(errors));

            // Act
            var result = await _controller.UpdateCustomer(model);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(400);
            result.Errors.Should().NotBeNull();
            result.Errors.Should().HaveCount(2);
            result.Errors[0].Field.Should().Be("customerCode");
            result.Errors[0].Message.Should().Be(Message.CustomerMessage.CUSTOMER_CODE_DUPLICATE);
        }

        [Fact]
        public async Task DeleteCustomer_ReturnsSuccessResponse_WhenCustomerDeleted()
        {
            // Arrange
            int customerId = 1;

            _mockCustomerService
                .Setup(s => s.DeleteCustomer(customerId, It.IsAny<int>()))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.DeleteCustomer(customerId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Message.Should().Be(Message.CustomerMessage.DELETE_CUSTOMER_SUCCESS);
            result.Data.Should().BeTrue();
            
            // Verify that correct userId was passed
            _mockCustomerService.Verify(s => s.DeleteCustomer(customerId, 1), Times.Once);
        }

        [Fact]
        public async Task DeleteCustomer_ReturnsFailureResponse_WhenCustomerNotFound()
        {
            // Arrange
            int customerId = 999;

            _mockCustomerService
                .Setup(s => s.DeleteCustomer(customerId, It.IsAny<int>()))
                .ThrowsAsync(new KeyNotFoundException(Message.CustomerMessage.CUSTOMER_NOT_FOUND));

            // Act
            var result = await _controller.DeleteCustomer(customerId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(404);
            result.Message.Should().Be(Message.CustomerMessage.CUSTOMER_NOT_FOUND);
        }

        [Fact]
        public async Task DeleteCustomer_ReturnsFailureResponse_WhenUserNotAuthorized()
        {
            // Arrange
            int customerId = 1;

            _mockCustomerService
                .Setup(s => s.DeleteCustomer(customerId, It.IsAny<int>()))
                .ThrowsAsync(new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED));

            // Act
            var result = await _controller.DeleteCustomer(customerId);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Code.Should().Be(403);
            result.Message.Should().Be(Message.CommonMessage.NOT_ALLOWED);
        }
    }
} 