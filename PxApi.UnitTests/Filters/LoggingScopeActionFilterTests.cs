using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;
using PxApi.Filters;
using PxApi.Utilities;

namespace PxApi.UnitTests.Filters
{
    [TestFixture]
    public class LoggingScopeActionFilterTests
    {
        private Mock<ILogger<LoggingScopeActionFilter>> _mockLogger = null!;
        private LoggingScopeActionFilter _filter = null!;

        [SetUp]
        public void SetUp()
        {
            _mockLogger = new Mock<ILogger<LoggingScopeActionFilter>>();
            _filter = new LoggingScopeActionFilter(_mockLogger.Object);
        }

        [Test]
        public async Task OnActionExecutionAsync_PushesControllerAndActionIntoScope()
        {
            // Arrange
            const string expectedController = "TestController";
            const string expectedAction = "TestAction";

            ControllerActionDescriptor descriptor = new()
            {
                ControllerName = expectedController,
                ActionName = expectedAction,
                ControllerTypeInfo = typeof(TestController).GetTypeInfo()
            };

            DefaultHttpContext httpContext = new();
            ActionContext actionContext = new(httpContext, new RouteData(), descriptor);
            TestController controller = new();

            ActionExecutingContext executingContext = new(actionContext, [], new Dictionary<string, object?>(), controller);
            bool nextCalled = false;
            ActionExecutionDelegate next = () =>
            {
                nextCalled = true;
                ActionExecutedContext executedContext = new(actionContext, [], controller);
                return Task.FromResult(executedContext);
            };

            Dictionary<string, object>? capturedScope = null;
            _mockLogger
                .Setup(x => x.BeginScope(It.IsAny<Dictionary<string, object>>()))
                .Callback<Dictionary<string, object>>(state => capturedScope = state)
                .Returns(Mock.Of<IDisposable>());

            // Act
            await _filter.OnActionExecutionAsync(executingContext, next);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(nextCalled, Is.True);
                Assert.That(capturedScope, Is.Not.Null);
                Assert.That(capturedScope!, Does.ContainKey(LoggerConsts.CONTROLLER));
                Assert.That(capturedScope![LoggerConsts.CONTROLLER], Is.EqualTo(nameof(TestController)));
                Assert.That(capturedScope, Does.ContainKey(LoggerConsts.ACTION));
                Assert.That(capturedScope[LoggerConsts.ACTION], Is.EqualTo(expectedAction));
            }
        }

        [Test]
        public async Task OnActionExecutionAsync_NonControllerDescriptor_UsesDisplayName()
        {
            // Arrange
            const string displayName = "CustomDisplayName";
            ActionDescriptor descriptor = new() { DisplayName = displayName };

            DefaultHttpContext httpContext = new();
            ActionContext actionContext = new(httpContext, new RouteData(), descriptor);
            TestController controller = new();

            ActionExecutingContext executingContext = new(actionContext, [], new Dictionary<string, object?>(), controller);
            ActionExecutionDelegate next = () =>
            {
                ActionExecutedContext executedContext = new(actionContext, [], controller);
                return Task.FromResult(executedContext);
            };

            Dictionary<string, object>? capturedScope = null;
            _mockLogger
                .Setup(x => x.BeginScope(It.IsAny<Dictionary<string, object>>()))
                .Callback<Dictionary<string, object>>(state => capturedScope = state)
                .Returns(Mock.Of<IDisposable>());

            // Act
            await _filter.OnActionExecutionAsync(executingContext, next);

            // Assert
            Assert.That(capturedScope![LoggerConsts.ACTION], Is.EqualTo(displayName));
        }

        [Test]
        public async Task OnActionExecutionAsync_NullDisplayName_UsesUnknown()
        {
            // Arrange
            ActionDescriptor descriptor = new() { DisplayName = null };

            DefaultHttpContext httpContext = new();
            ActionContext actionContext = new(httpContext, new RouteData(), descriptor);
            TestController controller = new();

            ActionExecutingContext executingContext = new(actionContext, [], new Dictionary<string, object?>(), controller);
            ActionExecutionDelegate next = () =>
            {
                ActionExecutedContext executedContext = new(actionContext, [], controller);
                return Task.FromResult(executedContext);
            };

            Dictionary<string, object>? capturedScope = null;
            _mockLogger
                .Setup(x => x.BeginScope(It.IsAny<Dictionary<string, object>>()))
                .Callback<Dictionary<string, object>>(state => capturedScope = state)
                .Returns(Mock.Of<IDisposable>());

            // Act
            await _filter.OnActionExecutionAsync(executingContext, next);

            // Assert
            Assert.That(capturedScope![LoggerConsts.ACTION], Is.EqualTo("Unknown"));
        }

        private sealed class TestController : ControllerBase { }
    }
}
