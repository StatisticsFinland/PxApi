namespace PxApi.Configuration
{
    /// <summary>
    /// Configuration for Application Insights integration.
    /// Connection string can be provided via configuration or the APPLICATIONINSIGHTS_CONNECTION_STRING environment variable.
    /// Log level filtering is controlled through the standard <c>Logging:ApplicationInsights:LogLevel</c> configuration section.
    /// </summary>
    public class ApplicationInsightsConfig
    {
        /// <summary>
        /// Application Insights connection string.
        /// Can be overridden by the APPLICATIONINSIGHTS_CONNECTION_STRING environment variable.
        /// </summary>
        public string? ConnectionString { get; }

        /// <summary>
        /// Initializes ApplicationInsights configuration from the provided configuration section.
        /// </summary>
        /// <param name="configurationSection">Configuration section containing ApplicationInsights settings.</param>
        /// <param name="envVarName">Variable name where the connection string can be overridden (default: "APPLICATIONINSIGHTS_CONNECTION_STRING").</param>
        public ApplicationInsightsConfig(IConfigurationSection configurationSection, string envVarName = "APPLICATIONINSIGHTS_CONNECTION_STRING")
        {
            ConnectionString = Environment.GetEnvironmentVariable(envVarName)
               ?? configurationSection.GetValue<string>(nameof(ConnectionString));
        }

        /// <summary>
        /// Returns true if Application Insights is configured (connection string is available).
        /// </summary>
        public bool IsEnabled => !string.IsNullOrEmpty(ConnectionString);
    }
}