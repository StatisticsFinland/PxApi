using Microsoft.Extensions.Configuration;
using PxApi.Configuration;

namespace PxApi.UnitTests.ConfigurationTests
{
    [TestFixture]
    public class AuthenticationConfigTests
    {
        [Test]
        public void AuthenticationConfig_WhenCacheApiKeyNotConfigured_ShouldBeDisabled()
        {
            // Arrange
            Dictionary<string, string?> configData = new()
            {
                ["Authentication:Cache:Hash"] = null,
                ["Authentication:Cache:Salt"] = null,
                ["Authentication:Cache:HeaderName"] = null
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Act
            AuthenticationConfig config = new(configuration.GetSection("Authentication"));

            // Assert
            Assert.Multiple(() => {
                Assert.That(config.IsEnabled, Is.False);
                Assert.That(config.Cache.IsEnabled, Is.False);
            });
        }

        [Test]
        public void AuthenticationConfig_WhenCacheApiKeyConfigured_ShouldBeEnabled()
        {
            // Arrange
            Dictionary<string, string?> configData = new()
            {
                ["Authentication:Cache:Hash"] = "test-hash",
                ["Authentication:Cache:Salt"] = "test-salt",
                ["Authentication:Cache:HeaderName"] = null
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Act
            AuthenticationConfig config = new(configuration.GetSection("Authentication"));

            // Assert
            Assert.Multiple(() => {
                Assert.That(config.IsEnabled, Is.True);
                Assert.That(config.Cache.IsEnabled, Is.True);
            });
        }

        [Test]
        public void CacheApiKeyConfig_WhenOnlyHashProvided_ShouldBeDisabled()
        {
            // Arrange
            Dictionary<string, string?> configData = new()
            {
                ["Authentication:Cache:Hash"] = "test-hash",
                ["Authentication:Cache:Salt"] = null,
                ["Authentication:Cache:HeaderName"] = null
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Act
            CacheApiKeyConfig config = new(configuration.GetSection("Authentication:Cache"));

            // Assert
            Assert.Multiple(() => {
                Assert.That(config.IsEnabled, Is.False);
                Assert.That(config.Hash, Is.EqualTo("test-hash"));
                Assert.That(config.Salt, Is.Null);
            });
        }

        [Test]
        public void CacheApiKeyConfig_WhenOnlySaltProvided_ShouldBeDisabled()
        {
            // Arrange
            Dictionary<string, string?> configData = new()
            {
                ["Authentication:Cache:Hash"] = null,
                ["Authentication:Cache:Salt"] = "test-salt",
                ["Authentication:Cache:HeaderName"] = null
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Act
            CacheApiKeyConfig config = new(configuration.GetSection("Authentication:Cache"));

            // Assert
            Assert.Multiple(() => {
                Assert.That(config.IsEnabled, Is.False);
                Assert.That(config.Hash, Is.Null);
                Assert.That(config.Salt, Is.EqualTo("test-salt"));
            });
        }

        [Test]
        public void CacheApiKeyConfig_WhenBothHashAndSaltProvided_ShouldBeEnabled()
        {
            // Arrange
            Dictionary<string, string?> configData = new()
            {
                ["Authentication:Cache:Hash"] = "test-hash",
                ["Authentication:Cache:Salt"] = "test-salt",
                ["Authentication:Cache:HeaderName"] = null
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Act
            CacheApiKeyConfig config = new(configuration.GetSection("Authentication:Cache"));

            // Assert
            Assert.Multiple(() => {
                Assert.That(config.IsEnabled, Is.True);
                Assert.That(config.Hash, Is.EqualTo("test-hash"));
                Assert.That(config.Salt, Is.EqualTo("test-salt"));
                Assert.That(config.HeaderName, Is.EqualTo("X-Cache-API-Key"));
            });
        }

        [Test]
        public void CacheApiKeyConfig_WhenCustomHeaderNameProvided_ShouldUseCustomHeader()
        {
            // Arrange
            Dictionary<string, string?> configData = new()
            {
                ["Authentication:Cache:Hash"] = "test-hash",
                ["Authentication:Cache:Salt"] = "test-salt",
                ["Authentication:Cache:HeaderName"] = "Custom-Cache-Key"
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Act
            CacheApiKeyConfig config = new(configuration.GetSection("Authentication:Cache"));

            // Assert
            Assert.That(config.HeaderName, Is.EqualTo("Custom-Cache-Key"));
        }

        [Test]
        public void CacheApiKeyConfig_WhenNoHeaderNameProvided_ShouldUseDefaultHeader()
        {
            // Arrange
            Dictionary<string, string?> configData = new()
            {
                ["Authentication:Cache:Hash"] = "test-hash",
                ["Authentication:Cache:Salt"] = "test-salt"
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Act
            CacheApiKeyConfig config = new(configuration.GetSection("Authentication:Cache"));

            // Assert
            Assert.That(config.HeaderName, Is.EqualTo("X-Cache-API-Key"));
        }

        [Test]
        public void CacheApiKeyConfig_WhenEnvironmentVariablesProvided_ShouldUseEnvironmentVariables()
        {
            // Arrange
            const string hashEnvVar = "Authentication__Cache__Hash";
            const string saltEnvVar = "Authentication__Cache__Salt";
            const string headerEnvVar = "Authentication__Cache__HeaderName";

            Environment.SetEnvironmentVariable(hashEnvVar, "env-hash");
            Environment.SetEnvironmentVariable(saltEnvVar, "env-salt");
            Environment.SetEnvironmentVariable(headerEnvVar, "Env-Cache-Key");

            try
            {
                Dictionary<string, string?> configData = new()
                {
                    ["Authentication:Cache:Hash"] = "config-hash",
                    ["Authentication:Cache:Salt"] = "config-salt",
                    ["Authentication:Cache:HeaderName"] = "Config-Cache-Key"
                };
                IConfiguration configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(configData)
                    .AddEnvironmentVariables()
                    .Build();

                // Act
                CacheApiKeyConfig config = new(configuration.GetSection("Authentication:Cache"));

                // Assert
                Assert.Multiple(() => {
                    Assert.That(config.IsEnabled, Is.True);
                    Assert.That(config.Hash, Is.EqualTo("env-hash"));
                    Assert.That(config.Salt, Is.EqualTo("env-salt"));
                    Assert.That(config.HeaderName, Is.EqualTo("Env-Cache-Key"));
                });
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable(hashEnvVar, null);
                Environment.SetEnvironmentVariable(saltEnvVar, null);
                Environment.SetEnvironmentVariable(headerEnvVar, null);
            }
        }

        [Test]
        public void CacheApiKeyConfig_WhenEnvironmentVariablesPartiallyProvided_ShouldFallbackToConfiguration()
        {
            // Arrange
            const string hashEnvVar = "Authentication__Cache__Hash";

            Environment.SetEnvironmentVariable(hashEnvVar, "env-hash");

            try
            {
                Dictionary<string, string?> configData = new()
                {
                    ["Authentication:Cache:Hash"] = "config-hash",
                    ["Authentication:Cache:Salt"] = "config-salt",
                    ["Authentication:Cache:HeaderName"] = "Config-Cache-Key"
                };
                IConfiguration configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(configData)
                    .AddEnvironmentVariables()
                    .Build();

                // Act
                CacheApiKeyConfig config = new(configuration.GetSection("Authentication:Cache"));

                // Assert
                Assert.Multiple(() => {
                    Assert.That(config.IsEnabled, Is.True);
                    Assert.That(config.Hash, Is.EqualTo("env-hash"));
                    Assert.That(config.Salt, Is.EqualTo("config-salt"));
                    Assert.That(config.HeaderName, Is.EqualTo("Config-Cache-Key"));
                });
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable(hashEnvVar, null);
            }
        }

        [Test]
        public void CacheApiKeyConfig_WhenOnlyEnvironmentHeaderNameProvided_ShouldFallbackToDefaultForMissingValues()
        {
            // Arrange
            const string headerEnvVar = "Authentication__Cache__HeaderName";

            Environment.SetEnvironmentVariable(headerEnvVar, "Env-Cache-Key");

            try
            {
                Dictionary<string, string?> configData = [];
                IConfiguration configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(configData)
                    .AddEnvironmentVariables()
                    .Build();

                // Act
                CacheApiKeyConfig config = new(configuration.GetSection("Authentication:Cache"));

                // Assert
                Assert.Multiple(() => {
                    Assert.That(config.IsEnabled, Is.False);
                    Assert.That(config.Hash, Is.Null);
                    Assert.That(config.Salt, Is.Null);
                    Assert.That(config.HeaderName, Is.EqualTo("Env-Cache-Key"));
                });
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable(headerEnvVar, null);
            }
        }
    }
}