using Microsoft.Extensions.Configuration;
using PxApi.Configuration;

namespace PxApi.UnitTests.ConfigurationTests
{
    [TestFixture]
    public class AuthenticationConfigTests
    {
        [Test]
        public void AuthenticationConfig_WhenNoApiKeysConfigured_ShouldBeDisabled()
        {
            // Arrange
            Dictionary<string, string?> configData = new()
            {
                ["Authentication:Cache:Key"] = null,
                ["Authentication:Databases:Key"] = null,
                ["Authentication:Tables:Key"] = null,
                ["Authentication:Metadata:Key"] = null,
                ["Authentication:Data:Key"] = null
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Act
            AuthenticationConfig config = new(configuration.GetSection("Authentication"));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(config.IsEnabled, Is.False);
                Assert.That(config.Cache.IsEnabled, Is.False);
                Assert.That(config.Databases.IsEnabled, Is.False);
                Assert.That(config.Tables.IsEnabled, Is.False);
                Assert.That(config.Metadata.IsEnabled, Is.False);
                Assert.That(config.Data.IsEnabled, Is.False);
            });
        }

        [Test]
        public void AuthenticationConfig_WhenCacheApiKeyConfigured_ShouldBeEnabled()
        {
            // Arrange
            Dictionary<string, string?> configData = new()
            {
                ["Authentication:Cache:Key"] = "test-key"
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Act
            AuthenticationConfig config = new(configuration.GetSection("Authentication"));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(config.IsEnabled, Is.True);
                Assert.That(config.Cache.IsEnabled, Is.True);
                Assert.That(config.Databases.IsEnabled, Is.False);
                Assert.That(config.Tables.IsEnabled, Is.False);
                Assert.That(config.Metadata.IsEnabled, Is.False);
                Assert.That(config.Data.IsEnabled, Is.False);
            });
        }

        [Test]
        public void AuthenticationConfig_WhenMultipleApiKeysConfigured_ShouldBeEnabled()
        {
            // Arrange
            Dictionary<string, string?> configData = new()
            {
                ["Authentication:Databases:Key"] = "db-key",
                ["Authentication:Data:Key"] = "data-key"
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Act
            AuthenticationConfig config = new(configuration.GetSection("Authentication"));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(config.IsEnabled, Is.True);
                Assert.That(config.Cache.IsEnabled, Is.False);
                Assert.That(config.Databases.IsEnabled, Is.True);
                Assert.That(config.Tables.IsEnabled, Is.False);
                Assert.That(config.Metadata.IsEnabled, Is.False);
                Assert.That(config.Data.IsEnabled, Is.True);
            });
        }

        [Test]
        public void CacheApiKeyConfig_WhenNoKeyProvided_ShouldBeDisabled()
        {
            // Arrange
            Dictionary<string, string?> configData = new()
            {
                ["Authentication:Cache:Key"] = null,
                ["Authentication:Cache:HeaderName"] = null
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Act
            CacheApiKeyConfig config = new(configuration.GetSection("Authentication:Cache"));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(config.IsEnabled, Is.False);
                Assert.That(config.Key, Is.Null);
            });
        }

        [Test]
        public void DatabasesApiKeyConfig_WhenKeyProvided_ShouldBeEnabled()
        {
            // Arrange
            Dictionary<string, string?> configData = new()
            {
                ["Authentication:Databases:Key"] = "test-key",
                ["Authentication:Databases:HeaderName"] = null
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Act
            DatabasesApiKeyConfig config = new(configuration.GetSection("Authentication:Databases"));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(config.IsEnabled, Is.True);
                Assert.That(config.Key, Is.EqualTo("test-key"));
                Assert.That(config.HeaderName, Is.EqualTo("X-Databases-API-Key"));
            });
        }

        [Test]
        public void TablesApiKeyConfig_WhenCustomHeaderNameProvided_ShouldUseCustomHeader()
        {
            // Arrange
            Dictionary<string, string?> configData = new()
            {
                ["Authentication:Tables:Key"] = "test-key",
                ["Authentication:Tables:HeaderName"] = "Custom-Tables-Key"
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Act
            TablesApiKeyConfig config = new(configuration.GetSection("Authentication:Tables"));

            // Assert
            Assert.That(config.HeaderName, Is.EqualTo("Custom-Tables-Key"));
        }

        [Test]
        public void MetadataApiKeyConfig_WhenNoHeaderNameProvided_ShouldUseDefaultHeader()
        {
            // Arrange
            Dictionary<string, string?> configData = new()
            {
                ["Authentication:Metadata:Key"] = "test-key"
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Act
            MetadataApiKeyConfig config = new(configuration.GetSection("Authentication:Metadata"));

            // Assert
            Assert.That(config.HeaderName, Is.EqualTo("X-Metadata-API-Key"));
        }

        [Test]
        public void DataApiKeyConfig_WhenDefaultValues_ShouldUseCorrectDefaults()
        {
            // Arrange
            Dictionary<string, string?> configData = new()
            {
                ["Authentication:Data:Key"] = "test-key"
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Act
            DataApiKeyConfig config = new(configuration.GetSection("Authentication:Data"));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(config.IsEnabled, Is.True);
                Assert.That(config.HeaderName, Is.EqualTo("X-Data-API-Key"));
            });
        }

        [Test]
        public void CacheApiKeyConfig_WhenEmptyKeyProvided_ShouldBeDisabled()
        {
            // Arrange
            Dictionary<string, string?> configData = new()
            {
                ["Authentication:Cache:Key"] = "",
                ["Authentication:Cache:HeaderName"] = null
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Act
            CacheApiKeyConfig config = new(configuration.GetSection("Authentication:Cache"));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(config.IsEnabled, Is.False);
                Assert.That(config.Key, Is.EqualTo(""));
            });
        }

        [Test]
        public void CacheApiKeyConfig_WhenKeyProvided_ShouldBeEnabled()
        {
            // Arrange
            Dictionary<string, string?> configData = new()
            {
                ["Authentication:Cache:Key"] = "test-key",
                ["Authentication:Cache:HeaderName"] = null
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Act
            CacheApiKeyConfig config = new(configuration.GetSection("Authentication:Cache"));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(config.IsEnabled, Is.True);
                Assert.That(config.Key, Is.EqualTo("test-key"));
                Assert.That(config.HeaderName, Is.EqualTo("X-Cache-API-Key"));
            });
        }

        [Test]
        public void CacheApiKeyConfig_WhenCustomHeaderNameProvided_ShouldUseCustomHeader()
        {
            // Arrange
            Dictionary<string, string?> configData = new()
            {
                ["Authentication:Cache:Key"] = "test-key",
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
                ["Authentication:Cache:Key"] = "test-key"
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
        public void AllApiKeyConfigs_WhenEnvironmentVariablesProvided_ShouldUseEnvironmentVariables()
        {
            // Arrange
            const string cacheKeyEnvVar = "Authentication__Cache__Key";
            const string databasesKeyEnvVar = "Authentication__Databases__Key";
            const string tablesKeyEnvVar = "Authentication__Tables__Key";
            const string metadataKeyEnvVar = "Authentication__Metadata__Key";
            const string dataKeyEnvVar = "Authentication__Data__Key";

            Environment.SetEnvironmentVariable(cacheKeyEnvVar, "env-cache-key");
            Environment.SetEnvironmentVariable(databasesKeyEnvVar, "env-databases-key");
            Environment.SetEnvironmentVariable(tablesKeyEnvVar, "env-tables-key");
            Environment.SetEnvironmentVariable(metadataKeyEnvVar, "env-metadata-key");
            Environment.SetEnvironmentVariable(dataKeyEnvVar, "env-data-key");

            try
            {
                Dictionary<string, string?> configData = new()
                {
                    ["Authentication:Cache:Key"] = "config-cache-key",
                    ["Authentication:Databases:Key"] = "config-databases-key",
                    ["Authentication:Tables:Key"] = "config-tables-key",
                    ["Authentication:Metadata:Key"] = "config-metadata-key",
                    ["Authentication:Data:Key"] = "config-data-key"
                };
                IConfiguration configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(configData)
                    .AddEnvironmentVariables()
                    .Build();

                // Act
                AuthenticationConfig config = new(configuration.GetSection("Authentication"));

                // Assert
                Assert.Multiple(() =>
                {
                    Assert.That(config.Cache.Key, Is.EqualTo("env-cache-key"));
                    Assert.That(config.Databases.Key, Is.EqualTo("env-databases-key"));
                    Assert.That(config.Tables.Key, Is.EqualTo("env-tables-key"));
                    Assert.That(config.Metadata.Key, Is.EqualTo("env-metadata-key"));
                    Assert.That(config.Data.Key, Is.EqualTo("env-data-key"));
                });
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable(cacheKeyEnvVar, null);
                Environment.SetEnvironmentVariable(databasesKeyEnvVar, null);
                Environment.SetEnvironmentVariable(tablesKeyEnvVar, null);
                Environment.SetEnvironmentVariable(metadataKeyEnvVar, null);
                Environment.SetEnvironmentVariable(dataKeyEnvVar, null);
            }
        }
    }
}