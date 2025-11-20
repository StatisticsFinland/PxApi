using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using Moq;
using PxApi.Controllers;
using PxApi.Exceptions;
using System.Reflection;
using System.Text.Json;

namespace PxApi.UnitTests.ControllerTests
{
    [TestFixture]
    public class ErrorControllerTests
    {
        private Mock<ILogger<ErrorController>> _mockLogger;
        private ErrorController _controller;
        private Mock<IExceptionHandlerPathFeature> _mockExceptionFeature;

        [SetUp]
        public void SetUp()
        {
            _mockLogger = new Mock<ILogger<ErrorController>>();
            _mockExceptionFeature = new Mock<IExceptionHandlerPathFeature>();

            _controller = new ErrorController(_mockLogger.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };
        }

        private void SetupExceptionFeature(Exception? exception, string path = "/test/path")
        {
            _mockExceptionFeature.SetupGet(x => x.Error).Returns(exception!);
            _mockExceptionFeature.SetupGet(x => x.Path).Returns(path);
            _controller.HttpContext.Features.Set(_mockExceptionFeature.Object);
        }

        private static string GetPropertyValue(object obj, string propertyName)
        {
            PropertyInfo? property = obj.GetType().GetProperty(propertyName);
            return property?.GetValue(obj)?.ToString() ?? string.Empty;
        }

        #region InvalidModelException Tests

        [Test]
        public void Error_WithInvalidModelException_ReturnsBadRequestWithCorrectMessageAndLogsInformation()
        {
            // Arrange
            const string requestPath = "/api/test";
            ModelStateDictionary modelState = new();
            modelState.AddModelError("field1", "Field is required");
            InvalidModelException exception = new(modelState, requestPath);
            SetupExceptionFeature(exception, requestPath);

            // Act
            IActionResult result = _controller.Error();

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
            BadRequestObjectResult? badRequest = result as BadRequestObjectResult;
            Assert.That(badRequest, Is.Not.Null);

            object? responseValue = badRequest.Value;
            Assert.That(responseValue, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(GetPropertyValue(responseValue, "error"), Is.EqualTo("Invalid model"));
                Assert.That(GetPropertyValue(responseValue, "message"), Is.EqualTo("The request model is invalid."));
                Assert.That(GetPropertyValue(responseValue, "path"), Is.EqualTo(requestPath));
            });

            // Verify logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Invalid model in {requestPath}")),
                    exception,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public void Error_WithInvalidModelExceptionCustomMessage_ReturnsBadRequestAndVerifiesExceptionProperties()
        {
            // Arrange
            const string requestPath = "/api/custom";
            const string customMessage = "Custom validation failed";
            ModelStateDictionary modelState = new();
            modelState.AddModelError("customField", "Custom error");
            InvalidModelException exception = new(customMessage, modelState, requestPath);
            SetupExceptionFeature(exception, requestPath);

            // Act
            IActionResult result = _controller.Error();

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());

            // Verify exception properties are set correctly
            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Is.EqualTo(customMessage));
                Assert.That(exception.ModelState, Is.EqualTo(modelState));
                Assert.That(exception.RequestPath, Is.EqualTo(requestPath));
            });

            // Verify logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Invalid model in {requestPath}")),
                    exception,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public void Error_WithInvalidModelExceptionDefaultConstructor_VerifiesDefaultMessage()
        {
            // Arrange
            const string requestPath = "/api/validation";
            ModelStateDictionary modelState = new();
            modelState.AddModelError("testField", "Test error message");
            InvalidModelException exception = new(modelState, requestPath);
            SetupExceptionFeature(exception, requestPath);

            // Act
            IActionResult result = _controller.Error();

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());

            // Verify exception properties are set correctly with default message
            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Is.EqualTo("Model validation failed"));
                Assert.That(exception.ModelState, Is.EqualTo(modelState));
                Assert.That(exception.RequestPath, Is.EqualTo(requestPath));
            });
        }

        #endregion

        #region JsonException Tests

        [Test]
        public void Error_WithJsonException_ReturnsBadRequestWithCorrectMessageAndLogsInformation()
        {
            // Arrange
            const string requestPath = "/api/json";
            JsonException exception = new("Invalid JSON format");
            SetupExceptionFeature(exception, requestPath);

            // Act
            IActionResult result = _controller.Error();

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
            BadRequestObjectResult? badRequest = result as BadRequestObjectResult;
            Assert.That(badRequest, Is.Not.Null);

            object? responseValue = badRequest.Value;
            Assert.That(responseValue, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(GetPropertyValue(responseValue, "error"), Is.EqualTo("Malformed JSON"));
                Assert.That(GetPropertyValue(responseValue, "message"), Is.EqualTo("The request contains malformed JSON."));
                Assert.That(GetPropertyValue(responseValue, "path"), Is.EqualTo(requestPath));
            });

            // Verify logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Malformed JSON in {requestPath}")),
                    exception,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region IOException Tests

        [Test]
        public void Error_WithIOException_ReturnsInternalServerErrorWithCorrectMessageAndLogsCritical()
        {
            // Arrange
            const string requestPath = "/api/file";
            IOException exception = new("Disk read error");
            SetupExceptionFeature(exception, requestPath);

            // Act
            IActionResult result = _controller.Error();

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            ObjectResult? objectResult = result as ObjectResult;
            Assert.That(objectResult, Is.Not.Null);
            Assert.That(objectResult.StatusCode, Is.EqualTo(500));

            object? responseValue = objectResult.Value;
            Assert.That(responseValue, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(GetPropertyValue(responseValue, "error"), Is.EqualTo("Internal server error"));
                Assert.That(GetPropertyValue(responseValue, "message"), Is.EqualTo("A system error occurred."));
            });

            // Verify logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Critical,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"An IO error occurred in {requestPath}")),
                    exception,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region General Exception Tests

        [Test]
        public void Error_WithGeneralException_ReturnsInternalServerErrorWithCorrectMessageAndLogsError()
        {
            // Arrange
            const string requestPath = "/api/general";
            ArgumentException exception = new("Invalid argument provided");
            SetupExceptionFeature(exception, requestPath);

            // Act
            IActionResult result = _controller.Error();

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            ObjectResult? objectResult = result as ObjectResult;
            Assert.That(objectResult, Is.Not.Null);
            Assert.That(objectResult.StatusCode, Is.EqualTo(500));

            object? responseValue = objectResult.Value;
            Assert.That(responseValue, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(GetPropertyValue(responseValue, "error"), Is.EqualTo("Internal server error"));
                Assert.That(GetPropertyValue(responseValue, "message"), Is.EqualTo("An unexpected error occurred."));
            });

            // Verify logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Error in {requestPath}")),
                    exception,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region No Exception Tests

        [Test]
        public void Error_WithoutExceptionOrNullFeature_ReturnsOkAndLogsError()
        {
            // Test both scenarios: null exception and null feature
            // Scenario 1: Exception feature exists but exception is null
            SetupExceptionFeature(null);
            IActionResult result1 = _controller.Error();
            Assert.That(result1, Is.InstanceOf<OkResult>());

            // Reset for scenario 2: No exception feature at all
            _controller.HttpContext.Features.Set<IExceptionHandlerPathFeature>(null!);
            IActionResult result2 = _controller.Error();
            Assert.That(result2, Is.InstanceOf<OkResult>());

            // Verify logging occurred for both scenarios (2 times total)
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error handler requested without exception")),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Exactly(2));
        }

        #endregion

        #region Path Handling Tests

        [Test]
        public void Error_WithDifferentPathScenarios_HandlesPathsCorrectly()
        {
            // Test null path scenario
            InvalidModelException exception1 = new(new ModelStateDictionary(), "/original/path");
            _mockExceptionFeature.SetupGet(x => x.Error).Returns(exception1);
            _mockExceptionFeature.SetupGet(x => x.Path).Returns((string?)null!);
            _controller.HttpContext.Features.Set(_mockExceptionFeature.Object);

            IActionResult result1 = _controller.Error();
            Assert.That(result1, Is.InstanceOf<BadRequestObjectResult>());

            BadRequestObjectResult? badRequest1 = result1 as BadRequestObjectResult;
            Assert.That(badRequest1, Is.Not.Null);
            object? responseValue1 = badRequest1.Value;
            Assert.That(GetPropertyValue(responseValue1!, "path"), Is.EqualTo("unknown"));

            // Test empty path scenario
            InvalidModelException exception2 = new(new ModelStateDictionary(), "/original/path");
            SetupExceptionFeature(exception2, "");

            IActionResult result2 = _controller.Error();
            Assert.That(result2, Is.InstanceOf<BadRequestObjectResult>());

            BadRequestObjectResult? badRequest2 = result2 as BadRequestObjectResult;
            Assert.That(badRequest2, Is.Not.Null);
            object? responseValue2 = badRequest2.Value;

            Assert.Multiple(() =>
            {
                Assert.That(responseValue1, Is.Not.Null);
                Assert.That(responseValue2, Is.Not.Null);
                Assert.That(GetPropertyValue(responseValue2!, "path"), Is.EqualTo(""));
            });
        }

        #endregion
    }
}