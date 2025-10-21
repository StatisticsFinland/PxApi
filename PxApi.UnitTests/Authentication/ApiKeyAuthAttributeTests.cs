using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using PxApi.Authentication;
using PxApi.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace PxApi.UnitTests.Authentication
{
    [TestFixture]
    public class ApiKeyAuthAttributeTests
    {
        private Mock<ILogger<ApiKeyAuthAttribute>> _mockLogger = null!;
        private Mock<IServiceProvider> _mockServiceProvider = null!;
        private Mock<HttpContext> _mockHttpContext = null!;
        private Mock<HttpRequest> _mockRequest = null!;
        private ActionExecutingContext _actionContext = null!;
        private ApiKeyAuthAttribute _attribute = null!;
        private bool _nextCalled;

        [SetUp]
        public void SetUp()
        {
            Dictionary<string, string?> configData = new()
            {
                ["RootUrl"] = "https://testurl.fi",
                ["Authentication:Cache:Hash"] = null,
                ["Authentication:Cache:Salt"] = null,
                ["Authentication:Cache:HeaderName"] = "X-API-KEY"
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();
            AppSettings.Load(configuration);

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

            _actionContext = new ActionExecutingContext(
                new ActionContext(_mockHttpContext.Object, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()),
                [],
                new Dictionary<string, object>()!,
                new object()
            );

            _attribute = new();
            _nextCalled = false;
        }

        private Task<ActionExecutedContext> NextDelegate()
        {
            _nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(
                _actionContext,
                [],
                new object()
            ));
        }

        private static void SetupAppSettingsWithApiKeyAuthEnabled(string headerName = "X-API-KEY")
        {
            const string testKey = "test-api-key";
            const string testSalt = "test-salt";
            string hashedKey = ComputeTestHash(testKey, testSalt);

            Dictionary<string, string?> configData = new()
            {
                ["RootUrl"] = "https://testurl.fi",
                ["Authentication:Cache:Hash"] = hashedKey,
                ["Authentication:Cache:Salt"] = testSalt,
                ["Authentication:Cache:HeaderName"] = headerName
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            AppSettings.Load(configuration);
        }

        private static string ComputeTestHash(string input, string salt)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input + salt));
            return Convert.ToBase64String(bytes);
        }

        [Test]
        public async Task OnActionExecutionAsync_WhenAuthenticationDisabled_ShouldProceed()
        {
            // Arrange
            Dictionary<string, string?> configData = new()
            {
                ["RootUrl"] = "https://testurl.fi",
                ["Authentication:Cache"] = null
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            AppSettings.Load(configuration);

            // Act
            await _attribute.OnActionExecutionAsync(_actionContext, NextDelegate);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(_nextCalled, Is.True);
                Assert.That(_actionContext.Result, Is.Null);
            });
        }

        [Test]
        public async Task OnActionExecutionAsync_WhenApiKeyAuthDisabled_ShouldProceed()
        {
            // Arrange
            Dictionary<string, string?> configData = new()
            {
                ["RootUrl"] = "https://testurl.fi",
                ["Authentication:Cache:Hash"] = null,
                ["Authentication:Cache:Salt"] = "test-salt",
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            AppSettings.Load(configuration);

            // Act
            await _attribute.OnActionExecutionAsync(_actionContext, NextDelegate);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(_nextCalled, Is.True);
                Assert.That(_actionContext.Result, Is.Null);
            });
        }

        [Test]
        public async Task OnActionExecutionAsync_WhenHeaderMissing_ShouldReturnUnauthorized()
        {
            // Arrange
            SetupAppSettingsWithApiKeyAuthEnabled();

            // Act
            await _attribute.OnActionExecutionAsync(_actionContext, NextDelegate);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(_nextCalled, Is.False);
                Assert.That(_actionContext.Result, Is.TypeOf<UnauthorizedObjectResult>());
                
                UnauthorizedObjectResult result = (UnauthorizedObjectResult)_actionContext.Result!;
                string expectedMessage = $"Missing X-API-KEY header";
                Assert.That(result.Value?.ToString(), Does.Contain(expectedMessage));
            });
        }

        [Test]
        public async Task OnActionExecutionAsync_WhenApiKeyEmpty_ShouldReturnUnauthorized()
        {
            // Arrange
            SetupAppSettingsWithApiKeyAuthEnabled();
            _mockRequest.Setup(r => r.Headers.TryGetValue("X-API-KEY", out It.Ref<StringValues>.IsAny))
                       .Returns(new TryGetValueDelegate((string key, out StringValues values) =>
                       {
                           values = new StringValues("");
                           return true;
                       }));

            // Act
            await _attribute.OnActionExecutionAsync(_actionContext, NextDelegate);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(_nextCalled, Is.False);
                Assert.That(_actionContext.Result, Is.TypeOf<UnauthorizedObjectResult>());
                
                UnauthorizedObjectResult result = (UnauthorizedObjectResult)_actionContext.Result!;
                Assert.That(result.Value?.ToString(), Does.Contain("Invalid API key"));
            });
        }

        [Test]
        public async Task OnActionExecutionAsync_WhenApiKeyInvalid_ShouldReturnUnauthorized()
        {
            // Arrange
            const string headerName = "X-API-KEY";
            SetupAppSettingsWithApiKeyAuthEnabled(headerName);
            _mockRequest.Setup(r => r.Headers.TryGetValue(headerName, out It.Ref<StringValues>.IsAny))
                       .Returns(new TryGetValueDelegate((string key, out StringValues values) =>
                       {
                           values = new StringValues("invalid-api-key");
                           return true;
                       }));

            // Act
            await _attribute.OnActionExecutionAsync(_actionContext, NextDelegate);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(_nextCalled, Is.False);
                Assert.That(_actionContext.Result, Is.TypeOf<UnauthorizedObjectResult>());
                
                UnauthorizedObjectResult result = (UnauthorizedObjectResult)_actionContext.Result!;
                Assert.That(result.Value?.ToString(), Does.Contain("Invalid API key"));
            });
        }

        [Test]
        public async Task OnActionExecutionAsync_WhenApiKeyValid_ShouldProceed()
        {
            // Arrange
            const string headerName = "X-API-KEY";
            const string validApiKey = "test-api-key";
            SetupAppSettingsWithApiKeyAuthEnabled(headerName);
            _mockRequest.Setup(r => r.Headers.TryGetValue(headerName, out It.Ref<StringValues>.IsAny))
                       .Returns(new TryGetValueDelegate((string key, out StringValues values) =>
                       {
                           values = new StringValues(validApiKey);
                           return true;
                       }));

            // Act
            await _attribute.OnActionExecutionAsync(_actionContext, NextDelegate);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(_nextCalled, Is.True);
                Assert.That(_actionContext.Result, Is.Null);
            });
        }

        [Test]
        public async Task OnActionExecutionAsync_WhenApiKeyValidWithCustomHeader_ShouldProceed()
        {
            // Arrange
            const string customHeaderName = "X-CUSTOM-API-KEY";
            const string validApiKey = "test-api-key";
            SetupAppSettingsWithApiKeyAuthEnabled(customHeaderName);
            _mockRequest.Setup(r => r.Headers.TryGetValue(customHeaderName, out It.Ref<StringValues>.IsAny))
                       .Returns(new TryGetValueDelegate((string key, out StringValues values) =>
                       {
                           values = new StringValues(validApiKey);
                           return true;
                       }));

            // Act
            await _attribute.OnActionExecutionAsync(_actionContext, NextDelegate);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(_nextCalled, Is.True);
                Assert.That(_actionContext.Result, Is.Null);
            });
        }

        private delegate bool TryGetValueDelegate(string key, out StringValues values);
    }
}