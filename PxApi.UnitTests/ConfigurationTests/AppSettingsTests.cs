using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PxApi.Configuration;
using PxApi.UnitTests.Utils;

namespace PxApi.UnitTests.ConfigurationTests
{
    [TestFixture]
    public class AppSettingsTests
    {
        [Test]
        public void AppSettings_WhenCacheConfigurationProvided_ShouldLoadCacheSettings()
        {
            // Arrange
            const long expectedCacheSize = 134217728; // 128 MB
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                TestConfigFactory.MountedDb(0, "TestDb", "datasource/root/"),
                new Dictionary<string, string?>
                {
                    ["Cache:MaxSizeBytes"] = expectedCacheSize.ToString(),
                    ["Cache:DefaultDataCellSize"] = "32",
                    ["Cache:DefaultUpdateTaskSize"] = "100",
                    ["Cache:DefaultTableGroupSize"] = "200",
                    ["Cache:DefaultFileListSize"] = "500000",
                    ["Cache:DefaultMetaSize"] = "300000"
                }
            );
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Act
            AppSettings.Load(configuration);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(AppSettings.Active.Cache.MaxSizeBytes, Is.EqualTo(expectedCacheSize));
                Assert.That(AppSettings.Active.Cache.DefaultDataCellSize, Is.EqualTo(32));
                Assert.That(AppSettings.Active.Cache.DefaultUpdateTaskSize, Is.EqualTo(100));
                Assert.That(AppSettings.Active.Cache.DefaultTableGroupSize, Is.EqualTo(200));
                Assert.That(AppSettings.Active.Cache.DefaultFileListSize, Is.EqualTo(500000));
                Assert.That(AppSettings.Active.Cache.DefaultMetaSize, Is.EqualTo(300000));
            });
        }

        [Test]
        public void AppSettings_WhenCacheConfigurationNotProvided_ShouldUseDefaultCacheSize()
        {
            // Arrange
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                TestConfigFactory.MountedDb(0, "TestDb", "datasource/root/")
            );
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Act
            AppSettings.Load(configuration);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(AppSettings.Active.Cache.MaxSizeBytes, Is.EqualTo(524288000)); // 512 MB default
                Assert.That(AppSettings.Active.Cache.DefaultDataCellSize, Is.EqualTo(16));
                Assert.That(AppSettings.Active.Cache.DefaultUpdateTaskSize, Is.EqualTo(50));
                Assert.That(AppSettings.Active.Cache.DefaultTableGroupSize, Is.EqualTo(100));
                Assert.That(AppSettings.Active.Cache.DefaultFileListSize, Is.EqualTo(350000));
                Assert.That(AppSettings.Active.Cache.DefaultMetaSize, Is.EqualTo(200000));
            });
        }

        [Test]
        public void AppSettings_WhenApplicationInsightsConfigurationNotProvided_ShouldLoadWithDisabledApplicationInsights()
        {
            // Arrange
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                TestConfigFactory.MountedDb(0, "TestDb", "datasource/root/")
            );
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Act
            AppSettings.Load(configuration);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(AppSettings.Active.ApplicationInsights.IsEnabled, Is.False);
                Assert.That(AppSettings.Active.ApplicationInsights.ConnectionString, Is.Null);
                Assert.That(AppSettings.Active.ApplicationInsights.MinimumLevel, Is.EqualTo(LogLevel.Information));
                Assert.That(AppSettings.Active.ApplicationInsights.EnableAdaptiveSampling, Is.False);
            });
        }

        [Test]
        public void AppSettings_WhenApplicationInsightsConfigurationProvided_ShouldLoadApplicationInsightsSettings()
        {
            // Arrange
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                TestConfigFactory.MountedDb(0, "TestDb", "datasource/root/"),
                new Dictionary<string, string?>
                {
                    ["ApplicationInsights:ConnectionString"] = "InstrumentationKey=test-key;IngestionEndpoint=https://test.com",
                    ["ApplicationInsights:MinimumLevel"] = "Debug",
                    ["ApplicationInsights:EnableAdaptiveSampling"] = "true"
                }
            );
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Act
            AppSettings.Load(configuration);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(AppSettings.Active.ApplicationInsights.IsEnabled, Is.True);
                Assert.That(AppSettings.Active.ApplicationInsights.ConnectionString, Is.EqualTo("InstrumentationKey=test-key;IngestionEndpoint=https://test.com"));
                Assert.That(AppSettings.Active.ApplicationInsights.MinimumLevel, Is.EqualTo(LogLevel.Debug));
                Assert.That(AppSettings.Active.ApplicationInsights.EnableAdaptiveSampling, Is.True);
            });
        }

        [Test]
        public void AppSettings_WhenApplicationInsightsEnvironmentVariableSet_ShouldPrioritizeEnvironmentVariable()
        {
            // Arrange
            const string envVarName = "APPLICATIONINSIGHTS_CONNECTION_STRING";
            const string envConnectionString = "InstrumentationKey=env-key;IngestionEndpoint=https://test.com";

            Environment.SetEnvironmentVariable(envVarName, envConnectionString);

            try
            {
                Dictionary<string, string?> configData = TestConfigFactory.Merge(
                    TestConfigFactory.Base(),
                    TestConfigFactory.MountedDb(0, "TestDb", "datasource/root/"),
                    new Dictionary<string, string?>
                    {
                        ["ApplicationInsights:ConnectionString"] = "InstrumentationKey=config-key;IngestionEndpoint=https://test.com",
                        ["ApplicationInsights:MinimumLevel"] = "Warning"
                    }
                );
                IConfiguration configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(configData)
                    .Build();

                // Act
                AppSettings.Load(configuration);

                // Assert - Environment variable should take priority over config
                Assert.Multiple(() =>
                {
                    Assert.That(AppSettings.Active.ApplicationInsights.IsEnabled, Is.True);
                    Assert.That(AppSettings.Active.ApplicationInsights.ConnectionString, Is.EqualTo(envConnectionString));
                    Assert.That(AppSettings.Active.ApplicationInsights.MinimumLevel, Is.EqualTo(LogLevel.Warning)); // Other settings from config should still work
                });
            }
            finally
            {
                Environment.SetEnvironmentVariable(envVarName, null);
            }
        }
    }
}