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
                ["Authentication:Cache:Hash"] = null,
                ["Authentication:Cache:Salt"] = null,
                ["Authentication:Databases:Hash"] = null,
                ["Authentication:Databases:Salt"] = null,
                ["Authentication:Tables:Hash"] = null,
                ["Authentication:Tables:Salt"] = null,
                ["Authentication:Metadata:Hash"] = null,
                ["Authentication:Metadata:Salt"] = null,
                ["Authentication:Data:Hash"] = null,
                ["Authentication:Data:Salt"] = null
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
                ["Authentication:Cache:Hash"] = "test-hash",
                ["Authentication:Cache:Salt"] = "test-salt"
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
                ["Authentication:Databases:Hash"] = "db-hash",
                ["Authentication:Databases:Salt"] = "db-salt",
                ["Authentication:Data:Hash"] = "data-hash",
                ["Authentication:Data:Salt"] = "data-salt"
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
            Assert.Multiple(() =>
            {
                Assert.That(config.IsEnabled, Is.False);
                Assert.That(config.Hash, Is.EqualTo("test-hash"));
                Assert.That(config.Salt, Is.Null);
            });
        }

        [Test]
        public void DatabasesApiKeyConfig_WhenBothHashAndSaltProvided_ShouldBeEnabled()
        {
            // Arrange
            Dictionary<string, string?> configData = new()
            {
                ["Authentication:Databases:Hash"] = "test-hash",
                ["Authentication:Databases:Salt"] = "test-salt",
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
                Assert.That(config.Hash, Is.EqualTo("test-hash"));
                Assert.That(config.Salt, Is.EqualTo("test-salt"));
                Assert.That(config.HeaderName, Is.EqualTo("X-Databases-API-Key"));
            });
        }

        [Test]
        public void TablesApiKeyConfig_WhenCustomHeaderNameProvided_ShouldUseCustomHeader()
        {
            // Arrange
            Dictionary<string, string?> configData = new()
            {
                ["Authentication:Tables:Hash"] = "test-hash",
                ["Authentication:Tables:Salt"] = "test-salt",
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
                ["Authentication:Metadata:Hash"] = "test-hash",
                ["Authentication:Metadata:Salt"] = "test-salt"
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
                ["Authentication:Data:Hash"] = "test-hash",
                ["Authentication:Data:Salt"] = "test-salt"
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
            Assert.Multiple(() =>
            {
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
            Assert.Multiple(() =>
            {
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
                .Build(); ;

            // Act
            CacheApiKeyConfig config = new(configuration.GetSection("Authentication:Cache"));

            // Assert
            Assert.That(config.HeaderName, Is.EqualTo("X-Cache-API-Key"));
        }

        [Test]
        public void AllApiKeyConfigs_WhenEnvironmentVariablesProvided_ShouldUseEnvironmentVariables()
        {
            // Arrange
            const string cacheHashEnvVar = "Authentication__Cache__Hash";
            const string databasesHashEnvVar = "Authentication__Databases__Hash";
            const string tablesHashEnvVar = "Authentication__Tables__Hash";
            const string metadataHashEnvVar = "Authentication__Metadata__Hash";
            const string dataHashEnvVar = "Authentication__Data__Hash";

            Environment.SetEnvironmentVariable(cacheHashEnvVar, "env-cache-hash");
            Environment.SetEnvironmentVariable(databasesHashEnvVar, "env-databases-hash");
            Environment.SetEnvironmentVariable(tablesHashEnvVar, "env-tables-hash");
            Environment.SetEnvironmentVariable(metadataHashEnvVar, "env-metadata-hash");
            Environment.SetEnvironmentVariable(dataHashEnvVar, "env-data-hash");

            try
            {
                Dictionary<string, string?> configData = new()
                {
                    ["Authentication:Cache:Hash"] = "config-cache-hash",
                    ["Authentication:Cache:Salt"] = "cache-salt",
                    ["Authentication:Databases:Hash"] = "config-databases-hash",
                    ["Authentication:Databases:Salt"] = "databases-salt",
                    ["Authentication:Tables:Hash"] = "config-tables-hash",
                    ["Authentication:Tables:Salt"] = "tables-salt",
                    ["Authentication:Metadata:Hash"] = "config-metadata-hash",
                    ["Authentication:Metadata:Salt"] = "metadata-salt",
                    ["Authentication:Data:Hash"] = "config-data-hash",
                    ["Authentication:Data:Salt"] = "data-salt"
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
                    Assert.That(config.Cache.Hash, Is.EqualTo("env-cache-hash"));
                    Assert.That(config.Databases.Hash, Is.EqualTo("env-databases-hash"));
                    Assert.That(config.Tables.Hash, Is.EqualTo("env-tables-hash"));
                    Assert.That(config.Metadata.Hash, Is.EqualTo("env-metadata-hash"));
                    Assert.That(config.Data.Hash, Is.EqualTo("env-data-hash"));
                });
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable(cacheHashEnvVar, null);
                Environment.SetEnvironmentVariable(databasesHashEnvVar, null);
                Environment.SetEnvironmentVariable(tablesHashEnvVar, null);
                Environment.SetEnvironmentVariable(metadataHashEnvVar, null);
                Environment.SetEnvironmentVariable(dataHashEnvVar, null);
            }
        }
    }
}