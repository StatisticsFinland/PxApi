using Microsoft.Extensions.Configuration;
using PxApi.Configuration;

namespace PxApi.UnitTests.ConfigurationTests
{
    [TestFixture]
    public class ApplicationInsightsConfigTests
    {
        private const string EnvVarName = "TEST_APPLICATIONINSIGHTS_CONNECTION_STRING";

        [Test]
        public void Constructor_WhenNoConfigurationProvided_ShouldBeDisabledWithDefaults()
        {
            // Arrange
            Dictionary<string, string?> configData = [];
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();
            IConfigurationSection section = configuration.GetSection("ApplicationInsights");

            // Act
            ApplicationInsightsConfig config = new(section, EnvVarName);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(config.IsEnabled, Is.False);
                Assert.That(config.ConnectionString, Is.Null);
                Assert.That(config.SamplingRatio, Is.EqualTo(1f));
                Assert.That(config.EnableTraceBasedLogsSampler, Is.False);
                Assert.That(config.IncludeAuditLogs, Is.False);
            }
        }

        [Test]
        public void Constructor_ExplicitTelemetryOptIns_AreRead()
        {
            IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApplicationInsights:SamplingRatio"] = "0.5",
                ["ApplicationInsights:EnableTraceBasedLogsSampler"] = "true",
                ["ApplicationInsights:IncludeAuditLogs"] = "true"
            }).Build();
            ApplicationInsightsConfig config = new(configuration.GetSection("ApplicationInsights"), EnvVarName);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(config.SamplingRatio, Is.EqualTo(0.5f));
                Assert.That(config.EnableTraceBasedLogsSampler, Is.True);
                Assert.That(config.IncludeAuditLogs, Is.True);
            }
        }

        [Test]
        public void Constructor_WhenConnectionStringInConfig_ShouldBeEnabledWithConnectionString()
        {
            // Arrange
            Dictionary<string, string?> configData = new()
            {
                ["ApplicationInsights:ConnectionString"] = "InstrumentationKey=test-key;IngestionEndpoint=https://test.com"
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();
            IConfigurationSection section = configuration.GetSection("ApplicationInsights");

            // Act
            ApplicationInsightsConfig config = new(section, EnvVarName);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(config.IsEnabled, Is.True);
                Assert.That(config.ConnectionString, Is.EqualTo("InstrumentationKey=test-key;IngestionEndpoint=https://test.com"));
            }
        }

        [Test]
        public void Constructor_WhenEnvironmentVariableSet_ShouldUseEnvironmentVariable()
        {
            // Arrange
            const string envConnectionString = "InstrumentationKey=env-key;IngestionEndpoint=https://test.com/env";

            Environment.SetEnvironmentVariable(EnvVarName, envConnectionString);

            try
            {
                Dictionary<string, string?> configData = new()
                {
                    ["ApplicationInsights:ConnectionString"] = "InstrumentationKey=config-key;IngestionEndpoint=https://test.com/config"
                };
                IConfiguration configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(configData)
                    .Build();
                IConfigurationSection section = configuration.GetSection("ApplicationInsights");

                // Act
                ApplicationInsightsConfig config = new(section, EnvVarName);

                // Assert - Environment variable should take priority
                using (Assert.EnterMultipleScope())
                {
                    Assert.That(config.IsEnabled, Is.True);
                    Assert.That(config.ConnectionString, Is.EqualTo(envConnectionString));
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(EnvVarName, null);
            }
        }

        [Test]
        public void Constructor_WhenOnlyEnvironmentVariableSet_ShouldUseEnvironmentVariable()
        {
            // Arrange
            const string envConnectionString = "InstrumentationKey=env-only-key;IngestionEndpoint=https://env-only.com";

            Environment.SetEnvironmentVariable(EnvVarName, envConnectionString);

            try
            {
                Dictionary<string, string?> configData = [];
                IConfiguration configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(configData)
                    .Build();
                IConfigurationSection section = configuration.GetSection("ApplicationInsights");

                // Act
                ApplicationInsightsConfig config = new(section, EnvVarName);

                // Assert
                using (Assert.EnterMultipleScope())
                {
                    Assert.That(config.IsEnabled, Is.True);
                    Assert.That(config.ConnectionString, Is.EqualTo(envConnectionString));
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(EnvVarName, null);
            }
        }

        [Test]
        public void Constructor_WhenEmptyConnectionStringInConfig_ShouldBeDisabled()
        {
            // Arrange
            Dictionary<string, string?> configData = new()
            {
                ["ApplicationInsights:ConnectionString"] = ""
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();
            IConfigurationSection section = configuration.GetSection("ApplicationInsights");

            // Act
            ApplicationInsightsConfig config = new(section, EnvVarName);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(config.IsEnabled, Is.False);
                Assert.That(config.ConnectionString, Is.EqualTo(""));
            }
        }
    }
}