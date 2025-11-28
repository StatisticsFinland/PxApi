using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using PxApi.Authentication;
using PxApi.UnitTests.Utils;

namespace PxApi.UnitTests.Authentication
{
    public class CacheController : ControllerBase { }
    public class DatabasesController : ControllerBase { }
    public class TablesController : ControllerBase { }
    public class MetadataController : ControllerBase { }
    public class DataController : ControllerBase { }

    [TestFixture]
    public class ApiKeyAuthAttributeTests
    {
        private Mock<ILogger<ApiKeyAuthAttribute>> _mockLogger = null!;
        private Mock<IServiceProvider> _mockServiceProvider = null!;
        private Mock<HttpContext> _mockHttpContext = null!;
        private Mock<HttpRequest> _mockRequest = null!;
        private ApiKeyAuthAttribute _attribute = null!;
        private bool _nextCalled;

        [SetUp]
        public void SetUp()
        {
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                new Dictionary<string, string?>
                {
                    ["Authentication:Cache:Key"] = null,
                    ["Authentication:Cache:HeaderName"] = "X-Cache-API-Key",
                    ["Authentication:Databases:Key"] = null,
                    ["Authentication:Databases:HeaderName"] = "X-Databases-API-Key",
                    ["Authentication:Tables:Key"] = null,
                    ["Authentication:Tables:HeaderName"] = "X-Tables-API-Key",
                    ["Authentication:Metadata:Key"] = null,
                    ["Authentication:Metadata:HeaderName"] = "X-Metadata-API-Key",
                    ["Authentication:Data:Key"] = null,
                    ["Authentication:Data:HeaderName"] = "X-Data-API-Key"
                }
                );
            TestConfigFactory.BuildAndLoad(configData);

            _mockLogger = new();
            _mockServiceProvider = new();
            _mockHttpContext = new();
            _mockRequest = new();

            _mockServiceProvider.Setup(s => s.GetService(typeof(ILogger<ApiKeyAuthAttribute>)))
                .Returns(_mockLogger.Object);

            _mockHttpContext.Setup(c => c.RequestServices).Returns(_mockServiceProvider.Object);
            _mockHttpContext.Setup(c => c.Request).Returns(_mockRequest.Object);

            HeaderDictionary headers = [];
            _mockRequest.Setup(r => r.Headers).Returns(headers);

            _attribute = new();
            _nextCalled = false;
        }

        private ActionExecutingContext CreateActionContext(object controller)
        {
            return new ActionExecutingContext(
                new ActionContext(_mockHttpContext.Object, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()),
                [],
                new Dictionary<string, object>()!,
                controller
                );
        }

        private Task<ActionExecutedContext> NextDelegate()
        {
            _nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(
                CreateActionContext(new CacheController()),
                [],
                new object()
                ));
        }

        private static void SetupAppSettingsWithApiKeyAuthEnabled(string controllerType, string headerName)
        {
            const string testKey = "test-api-key";

            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                new Dictionary<string, string?>
                {
                    [$"Authentication:{controllerType}:Key"] = testKey,
                    [$"Authentication:{controllerType}:HeaderName"] = headerName
                }
                );
            TestConfigFactory.BuildAndLoad(configData);
        }

        [Test]
        public async Task OnActionExecutionAsync_WhenAuthenticationDisabled_ShouldProceed()
        {
            // Arrange
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                new Dictionary<string, string?>
                {
                    ["Authentication:Cache"] = null,
                    ["Authentication:Databases"] = null,
                    ["Authentication:Tables"] = null,
                    ["Authentication:Metadata"] = null,
                    ["Authentication:Data"] = null
                }
                );
            TestConfigFactory.BuildAndLoad(configData);
            ActionExecutingContext actionContext = CreateActionContext(new CacheController());

            // Act
            await _attribute.OnActionExecutionAsync(actionContext, NextDelegate);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(_nextCalled, Is.True);
                Assert.That(actionContext.Result, Is.Null);
            });
        }

        [Test]
        public async Task OnActionExecutionAsync_WhenCacheApiKeyAuthDisabled_ShouldProceed()
        {
            // Arrange
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                new Dictionary<string, string?>
                {
                    ["Authentication:Cache:Key"] = null,
                }
                );
            TestConfigFactory.BuildAndLoad(configData);
            ActionExecutingContext actionContext = CreateActionContext(new CacheController());

            // Act
            await _attribute.OnActionExecutionAsync(actionContext, NextDelegate);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(_nextCalled, Is.True);
                Assert.That(actionContext.Result, Is.Null);
            });
        }

        [Test]
        public async Task OnActionExecutionAsync_WhenDatabasesControllerHeaderMissing_ShouldReturnUnauthorized()
        {
            // Arrange
            const string headerName = "X-Databases-API-Key";
            SetupAppSettingsWithApiKeyAuthEnabled("Databases", headerName);
            ActionExecutingContext actionContext = CreateActionContext(new DatabasesController());

            // Act
            await _attribute.OnActionExecutionAsync(actionContext, NextDelegate);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(_nextCalled, Is.False);
                Assert.That(actionContext.Result, Is.TypeOf<UnauthorizedObjectResult>());

                UnauthorizedObjectResult result = (UnauthorizedObjectResult)actionContext.Result!;
                string expectedMessage = $"Missing {headerName} header";
                Assert.That(result.Value?.ToString(), Does.Contain(expectedMessage));
            });
        }

        [Test]
        public async Task OnActionExecutionAsync_WhenTablesControllerApiKeyEmpty_ShouldReturnUnauthorized()
        {
            // Arrange
            const string headerName = "X-Tables-API-Key";
            SetupAppSettingsWithApiKeyAuthEnabled("Tables", headerName);
            ActionExecutingContext actionContext = CreateActionContext(new TablesController());
            _mockRequest.Setup(r => r.Headers.TryGetValue(headerName, out It.Ref<StringValues>.IsAny))
                .Returns(new TryGetValueDelegate((string key, out StringValues values) =>
                {
                    values = new StringValues("");
                    return true;
                }));

            // Act
            await _attribute.OnActionExecutionAsync(actionContext, NextDelegate);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(_nextCalled, Is.False);
                Assert.That(actionContext.Result, Is.TypeOf<UnauthorizedObjectResult>());

                UnauthorizedObjectResult result = (UnauthorizedObjectResult)actionContext.Result!;
                Assert.That(result.Value?.ToString(), Does.Contain("Invalid API key"));
            });
        }

        [Test]
        public async Task OnActionExecutionAsync_WhenMetadataControllerApiKeyInvalid_ShouldReturnUnauthorized()
        {
            // Arrange
            const string headerName = "X-Metadata-API-Key";
            SetupAppSettingsWithApiKeyAuthEnabled("Metadata", headerName);
            ActionExecutingContext actionContext = CreateActionContext(new MetadataController());
            _mockRequest.Setup(r => r.Headers.TryGetValue(headerName, out It.Ref<StringValues>.IsAny))
                .Returns(new TryGetValueDelegate((string key, out StringValues values) =>
                {
                    values = new StringValues("invalid-api-key");
                    return true;
                }));

            // Act
            await _attribute.OnActionExecutionAsync(actionContext, NextDelegate);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(_nextCalled, Is.False);
                Assert.That(actionContext.Result, Is.TypeOf<UnauthorizedObjectResult>());

                UnauthorizedObjectResult result = (UnauthorizedObjectResult)actionContext.Result!;
                Assert.That(result.Value?.ToString(), Does.Contain("Invalid API key"));
            });
        }

        [Test]
        public async Task OnActionExecutionAsync_WhenDataControllerApiKeyValid_ShouldProceed()
        {
            // Arrange
            const string headerName = "X-Data-API-Key";
            const string validApiKey = "test-api-key";
            SetupAppSettingsWithApiKeyAuthEnabled("Data", headerName);
            ActionExecutingContext actionContext = CreateActionContext(new DataController());
            _mockRequest.Setup(r => r.Headers.TryGetValue(headerName, out It.Ref<StringValues>.IsAny))
                .Returns(new TryGetValueDelegate((string key, out StringValues values) =>
                {
                    values = new StringValues(validApiKey);
                    return true;
                }));

            // Act
            await _attribute.OnActionExecutionAsync(actionContext, NextDelegate);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(_nextCalled, Is.True);
                Assert.That(actionContext.Result, Is.Null);
            });
        }

        [Test]
        public async Task OnActionExecutionAsync_WhenCacheControllerApiKeyValid_ShouldProceed()
        {
            // Arrange
            const string headerName = "X-Cache-API-Key";
            const string validApiKey = "test-api-key";
            SetupAppSettingsWithApiKeyAuthEnabled("Cache", headerName);
            ActionExecutingContext actionContext = CreateActionContext(new CacheController());
            _mockRequest.Setup(r => r.Headers.TryGetValue(headerName, out It.Ref<StringValues>.IsAny))
                .Returns(new TryGetValueDelegate((string key, out StringValues values) =>
                {
                    values = new StringValues(validApiKey);
                    return true;
                }));

            // Act
            await _attribute.OnActionExecutionAsync(actionContext, NextDelegate);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(_nextCalled, Is.True);
                Assert.That(actionContext.Result, Is.Null);
            });
        }

        [Test]
        public async Task OnActionExecutionAsync_WhenUnknownController_ShouldProceed()
        {
            // Arrange
            ActionExecutingContext actionContext = CreateActionContext(new object()); // Unknown controller type

            // Act
            await _attribute.OnActionExecutionAsync(actionContext, NextDelegate);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(_nextCalled, Is.True);
                Assert.That(actionContext.Result, Is.Null);
            });
        }

        private delegate bool TryGetValueDelegate(string key, out StringValues values);
    }
}