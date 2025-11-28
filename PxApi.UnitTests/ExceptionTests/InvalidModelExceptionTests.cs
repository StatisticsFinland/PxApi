using Microsoft.AspNetCore.Mvc.ModelBinding;
using PxApi.Exceptions;

namespace PxApi.UnitTests.ExceptionTests
{
    /// <summary>
    /// Unit tests for the InvalidModelException class.
    /// Tests the exception constructors and property initialization.
    /// </summary>
    [TestFixture]
    public class InvalidModelExceptionTests
    {
        [Test]
        public void DefaultConstructor_SetsPropertiesCorrectly()
        {
            // Arrange
            const string requestPath = "/api/validation";
            ModelStateDictionary modelState = new();
            modelState.AddModelError("testField", "Test error message");

            // Act
            InvalidModelException exception = new(modelState, requestPath);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Is.EqualTo("Model validation failed"));
                Assert.That(exception.ModelState, Is.EqualTo(modelState));
                Assert.That(exception.RequestPath, Is.EqualTo(requestPath));
            });
        }

        [Test]
        public void CustomMessageConstructor_SetsPropertiesCorrectly()
        {
            // Arrange
            const string customMessage = "Custom validation error occurred";
            const string requestPath = "/api/custom-validation";
            ModelStateDictionary modelState = new();
            modelState.AddModelError("customField", "Custom field error");

            // Act
            InvalidModelException exception = new(customMessage, modelState, requestPath);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Is.EqualTo(customMessage));
                Assert.That(exception.ModelState, Is.EqualTo(modelState));
                Assert.That(exception.RequestPath, Is.EqualTo(requestPath));
            });
        }

        [Test]
        public void Constructor_WithNullModelState_HandlesGracefully()
        {
            // Arrange
            const string requestPath = "/api/test";
            ModelStateDictionary modelState = new();

            // Act & Assert - Should not throw
            Assert.DoesNotThrow(() =>
            {
                InvalidModelException exception = new(modelState, requestPath);
                Assert.That(exception.ModelState, Is.EqualTo(modelState));
            });
        }

        [Test]
        public void Constructor_WithEmptyRequestPath_HandlesGracefully()
        {
            // Arrange
            const string requestPath = "";
            ModelStateDictionary modelState = new();
            modelState.AddModelError("field", "error");

            // Act & Assert - Should not throw
            Assert.DoesNotThrow(() =>
            {
                InvalidModelException exception = new(modelState, requestPath);
                Assert.That(exception.RequestPath, Is.EqualTo(requestPath));
            });
        }

        [Test]
        public void Exception_InheritsFromSystemException()
        {
            // Arrange
            ModelStateDictionary modelState = new();
            const string requestPath = "/api/test";

            // Act
            InvalidModelException exception = new(modelState, requestPath);

            // Assert
            Assert.That(exception, Is.InstanceOf<Exception>());
        }

        [Test]
        public void Exception_WithMultipleModelErrors_PreservesAllErrors()
        {
            // Arrange
            const string requestPath = "/api/multi-error";
            ModelStateDictionary modelState = new();
            modelState.AddModelError("field1", "Error 1");
            modelState.AddModelError("field2", "Error 2");
            modelState.AddModelError("field1", "Another error for field1");

            // Act
            InvalidModelException exception = new(modelState, requestPath);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(exception.ModelState.ErrorCount, Is.EqualTo(3));
                Assert.That(exception.ModelState.Keys, Contains.Item("field1"));
                Assert.That(exception.ModelState.Keys, Contains.Item("field2"));
                Assert.That(exception.ModelState["field1"]?.Errors, Has.Count.EqualTo(2));
                Assert.That(exception.ModelState["field2"]?.Errors, Has.Count.EqualTo(1));
            });
        }
    }
}