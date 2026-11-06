using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using PxApi.Filters;
using PxApi.Services;

namespace PxApi.UnitTests.Filters
{
    [TestFixture]
    public class OperationCanceledExceptionFilterTests
    {
        private Mock<ILogger<OperationCanceledExceptionFilter>> _mockLogger = null!;
        private OperationCanceledExceptionFilter _filter = null!;

        [SetUp]
        public void SetUp()
        {
            _mockLogger = new Mock<ILogger<OperationCanceledExceptionFilter>>();
            _filter = new OperationCanceledExceptionFilter(_mockLogger.Object);
        }

        private static ExceptionContext CreateExceptionContext(Exception exception)
        {
            DefaultHttpContext httpContext = new();
            httpContext.Request.Path = "/data/databases/testdb/tables/testtable";
            ActionContext actionContext = new(httpContext, new RouteData(), new ActionDescriptor());
            ExceptionContext context = new(actionContext, [])
            {
                Exception = exception
            };
            return context;
        }

        [Test]
        public void OnException_WithOperationCanceledException_Returns499AndMarksHandled()
        {
            // Arrange
            ExceptionContext context = CreateExceptionContext(new OperationCanceledException());

            // Act
            _filter.OnException(context);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(context.ExceptionHandled, Is.True);
                Assert.That(context.Result, Is.InstanceOf<StatusCodeResult>());
                StatusCodeResult statusCodeResult = (StatusCodeResult)context.Result!;
                Assert.That(statusCodeResult.StatusCode, Is.EqualTo(499));
            }
        }

        [Test]
        public void OnException_WithTaskCanceledException_Returns499AndMarksHandled()
        {
            // Arrange
            ExceptionContext context = CreateExceptionContext(new TaskCanceledException());

            // Act
            _filter.OnException(context);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(context.ExceptionHandled, Is.True);
                Assert.That(context.Result, Is.InstanceOf<StatusCodeResult>());
                StatusCodeResult statusCodeResult = (StatusCodeResult)context.Result!;
                Assert.That(statusCodeResult.StatusCode, Is.EqualTo(499));
            }
        }

        [Test]
        public void OnException_UntrackedOperationCanceledException_LogsDebug()
        {
            // Arrange
            ExceptionContext context = CreateExceptionContext(new OperationCanceledException());

            // Act
            _filter.OnException(context);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Request was cancelled")),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OnException_TrackedCancellation_Returns499WithoutDebug(bool taskCancelled)
        {
            Exception exception = taskCancelled ? new TaskCanceledException() : new OperationCanceledException();
            ExceptionContext context = CreateExceptionContext(exception);
            context.HttpContext.Features.Set(new QueryObservation(new ConfigurationBuilder().Build()));

            _filter.OnException(context);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(context.ExceptionHandled, Is.True);
                Assert.That(context.Result, Is.InstanceOf<StatusCodeResult>());
                Assert.That(((StatusCodeResult)context.Result!).StatusCode, Is.EqualTo(499));
            }
            _mockLogger.VerifyNoOtherCalls();
        }

        [Test]
        public void OnException_WithOtherException_DoesNotHandle()
        {
            // Arrange
            ExceptionContext context = CreateExceptionContext(new InvalidOperationException("test error"));

            // Act
            _filter.OnException(context);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(context.ExceptionHandled, Is.False);
                Assert.That(context.Result, Is.Null);
            }
        }
    }
}
