using Microsoft.Extensions.Configuration;
using PxApi.Configuration;

namespace PxApi.UnitTests.ConfigurationTests
{
    [TestFixture]
    public class CacheSettingsTests
    {
        [Test]
        public void CacheSettings_WhenMaxSizeBytesConfigured_ShouldUseConfiguredValue()
        {
            // Arrange
            const long expectedCacheSize = 134217728; // 128 MB
            Dictionary<string, string?> configData = new()
            {
                ["Cache:MaxSizeBytes"] = expectedCacheSize.ToString()
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Act
            MemoryCacheConfig cacheSettings = new(configuration.GetSection("Cache"));

            // Assert
            Assert.That(cacheSettings.MaxSizeBytes, Is.EqualTo(expectedCacheSize));
        }

        [Test]
        public void CacheSettings_WhenMaxSizeBytesNotConfigured_ShouldUseDefaultValue()
        {
            // Arrange
            Dictionary<string, string?> configData = [];
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Act
            MemoryCacheConfig cacheSettings = new(configuration.GetSection("Cache"));

            // Assert
            Assert.That(cacheSettings.MaxSizeBytes, Is.EqualTo(524288000)); // 512 MB default
        }

        [Test]
        public void CacheSettings_WhenDefaultSizesNotConfigured_ShouldUseDefaultValues()
        {
            // Arrange
            Dictionary<string, string?> configData = [];
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Act
            MemoryCacheConfig cacheSettings = new(configuration.GetSection("Cache"));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(cacheSettings.DefaultDataCellSize, Is.EqualTo(16));
                Assert.That(cacheSettings.DefaultUpdateTaskSize, Is.EqualTo(50));
                Assert.That(cacheSettings.DefaultTableGroupSize, Is.EqualTo(100));
                Assert.That(cacheSettings.DefaultFileListSize, Is.EqualTo(350000));
                Assert.That(cacheSettings.DefaultMetaSize, Is.EqualTo(200000));
            });
        }

        [Test]
        public void CacheSettings_WhenDefaultSizesConfigured_ShouldUseConfiguredValues()
        {
            // Arrange
            Dictionary<string, string?> configData = new()
            {
                ["Cache:DefaultDataCellSize"] = "32",
                ["Cache:DefaultUpdateTaskSize"] = "100",
                ["Cache:DefaultTableGroupSize"] = "200",
                ["Cache:DefaultFileListSize"] = "500000",
                ["Cache:DefaultMetaSize"] = "300000"
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Act
            MemoryCacheConfig cacheSettings = new(configuration.GetSection("Cache"));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(cacheSettings.DefaultDataCellSize, Is.EqualTo(32));
                Assert.That(cacheSettings.DefaultUpdateTaskSize, Is.EqualTo(100));
                Assert.That(cacheSettings.DefaultTableGroupSize, Is.EqualTo(200));
                Assert.That(cacheSettings.DefaultFileListSize, Is.EqualTo(500000));
                Assert.That(cacheSettings.DefaultMetaSize, Is.EqualTo(300000));
            });
        }

        [Test]
        public void CacheSettings_WhenInvalidMaxSizeBytesConfigured_ShouldThrow()
        {
            // Arrange
            Dictionary<string, string?> configData = new()
            {
                ["Cache:MaxSizeBytes"] = "invalid_value"
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => new MemoryCacheConfig(configuration.GetSection("Cache")));
        }

        [Test]
        public void CacheSettings_WhenZeroMaxSizeBytesConfigured_ShouldUseConfiguredValue()
        {
            // Arrange
            Dictionary<string, string?> configData = new()
            {
                ["Cache:MaxSizeBytes"] = "0"
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Act
            MemoryCacheConfig cacheSettings = new(configuration.GetSection("Cache"));

            // Assert
            Assert.That(cacheSettings.MaxSizeBytes, Is.EqualTo(0));
        }

        [Test]
        public void CacheSettings_WhenEnvironmentVariableProvided_ShouldUseEnvironmentVariable()
        {
            // Arrange
            const string envVarName = "Cache__MaxSizeBytes";
            const long expectedCacheSize = 1073741824; // 1 GB

            Environment.SetEnvironmentVariable(envVarName, expectedCacheSize.ToString());

            try
            {
                Dictionary<string, string?> configData = new()
                {
                    ["Cache:MaxSizeBytes"] = "268435456" // 256 MB in config
                };
                IConfiguration configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(configData)
                    .AddEnvironmentVariables()
                    .Build();

                // Act
                MemoryCacheConfig cacheSettings = new(configuration.GetSection("Cache"));

                // Assert
                Assert.That(cacheSettings.MaxSizeBytes, Is.EqualTo(expectedCacheSize));
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable(envVarName, null);
            }
        }
    }
}