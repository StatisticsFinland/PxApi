using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PxApi.Services;
using System.Net;
using System.Security.Claims;

namespace PxApi.UnitTests.Services
{
    [TestFixture]
    public class AuditLogServiceTests
    {
        private TestLogger _testLogger = null!;
        private HttpContextAccessor _httpContextAccessor = null!;

        private sealed class TestLogger : ILogger<AuditLogService>
        {
            public List<(LogLevel Level, EventId EventId, IReadOnlyList<KeyValuePair<string, object>> State, Exception? Exception)> Entries { get; } = [];
            public Dictionary<string, string>? LastScope { get; private set; }

            private sealed class ScopeDisposable(Action onDispose) : IDisposable
            {
                private readonly Action _onDispose = onDispose;

                public void Dispose() { _onDispose(); }
            }

            IDisposable ILogger.BeginScope<TState>(TState state)
            {
                if (state is IDictionary<string, string> dict)
                {
                    LastScope = new Dictionary<string, string>(dict);
                }
                return new ScopeDisposable(() => { });
            }

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (state is IEnumerable<KeyValuePair<string, object>> kvps)
                {
                    IReadOnlyList<KeyValuePair<string, object>> collected = [.. kvps];
                    Entries.Add((logLevel, eventId, collected, exception));
                }
            }
        }

        [SetUp]
        public void SetUp()
        {
            _testLogger = new TestLogger();
            _httpContextAccessor = new HttpContextAccessor();
        }

        private static IConfiguration BuildConfig(bool enabled, params string[] headers)
        {
            Dictionary<string, string?> values = new()
            {
                ["LogOptions:AuditLog:Enabled"] = enabled.ToString().ToLowerInvariant()
            };
            for (int i = 0; i < headers.Length; i++)
            {
                values[$"LogOptions:AuditLog:Headers:{i}"] = headers[i];
            }
            IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
            return configuration;
        }

        [Test]
        public void LogAuditEvent_Disabled_DoesNotLog()
        {
            // Arrange
            IConfiguration configuration = BuildConfig(false, "X-Correlation-Id");
            AuditLogService service = new(_httpContextAccessor, _testLogger, configuration);

            // Act
            service.LogAuditEvent("GetData", "db/table");

            // Assert
            Assert.That(_testLogger.Entries, Is.Empty);
        }

        [Test]
        public void LogAuditEvent_NoHttpContext_DoesNotLog()
        {
            // Arrange
            IConfiguration configuration = BuildConfig(true, "X-Correlation-Id");
            AuditLogService service = new(_httpContextAccessor, _testLogger, configuration);

            // Act
            service.LogAuditEvent("GetData", "db/table");

            // Assert
            Assert.That(_testLogger.Entries, Is.Empty);
        }

        [Test]
        public void LogAuditEvent_Enabled_LogsExpectedValuesAndScope()
        {
            // Arrange
            IConfiguration configuration = BuildConfig(true, "X-Correlation-Id", "X-Request-Id");
            _httpContextAccessor.HttpContext = new DefaultHttpContext();
            _httpContextAccessor.HttpContext.Request.Headers["X-Correlation-Id"] = "abc123";
            _httpContextAccessor.HttpContext.Request.Headers["X-Ignored"] = "should-not-be-in-scope";
            ClaimsIdentity identity = new([new Claim(ClaimTypes.Name, "TestUser")], "TestAuth");
            _httpContextAccessor.HttpContext.User = new ClaimsPrincipal(identity);
            _httpContextAccessor.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");

            AuditLogService service = new(_httpContextAccessor, _testLogger, configuration);

            // Act
            service.LogAuditEvent("GetData", "db/table");

            // Assert scope
            Dictionary<string, string>? scope = _testLogger.LastScope;
            Assert.That(scope, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(scope!.ContainsKey("Category"), Is.True);
                Assert.That(scope["Category"], Is.EqualTo("Audit"));
                Assert.That(scope.ContainsKey("X-Correlation-Id"), Is.True);
                Assert.That(scope["X-Correlation-Id"], Is.EqualTo("abc123"));
                Assert.That(scope.ContainsKey("X-Ignored"), Is.False);
                Assert.That(scope.ContainsKey("X-Request-Id"), Is.False); // Header whitelisted but absent
            });

            // Assert log content
            (LogLevel Level, EventId EventId, IReadOnlyList<KeyValuePair<string, object>> State, Exception? Exception) infoEntry
                = _testLogger.Entries.FirstOrDefault(e => e.Level == LogLevel.Information);
            Assert.That(infoEntry.State, Is.Not.Null);
            Dictionary<string, object> dict = infoEntry.State.ToDictionary(k => k.Key, v => v.Value);
            Assert.Multiple(() =>
            {
                Assert.That(dict.ContainsKey("Action"), Is.True);
                Assert.That(dict.ContainsKey("Resource"), Is.True);
                Assert.That(dict.ContainsKey("User"), Is.True);
                Assert.That(dict.ContainsKey("ClientIP"), Is.True);
                Assert.That(dict["Action"], Is.EqualTo("GetData"));
                Assert.That(dict["Resource"], Is.EqualTo("db/table"));
                Assert.That(dict["User"], Is.EqualTo("TestUser"));
                Assert.That(dict["ClientIP"], Is.EqualTo("127.0.0.1"));
            });
        }
    }
}
