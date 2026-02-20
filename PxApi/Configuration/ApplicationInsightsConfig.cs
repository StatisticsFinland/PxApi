namespace PxApi.Configuration
{
    /// <summary>
    /// Configuration for Application Insights integration.
    /// </summary>
    public class ApplicationInsightsConfig
    {
        /// <summary>
        /// Application Insights connection string.
        /// Can be overridden by the APPLICATIONINSIGHTS_CONNECTION_STRING environment variable.
        /// </summary>
        public string? ConnectionString { get; }

        /// <summary>
        /// Minimum log level to send to Application Insights.
        /// Defaults to Information if not specified.
        /// </summary>
        public LogLevel MinimumLevel { get; }

        /// <summary>
        /// Whether adaptive sampling is enabled.
        /// Defaults to false to ensure all configured logs are captured.
        /// </summary>
        public bool EnableAdaptiveSampling { get; }

        /// <summary>
        /// Initializes ApplicationInsights configuration from the provided configuration section.
        /// </summary>
        /// <param name="configurationSection">Configuration section containing ApplicationInsights settings.</param>
        /// <param name="envVarName">Variable name where the connection string can be overridden (default: "APPLICATIONINSIGHTS_CONNECTION_STRING").</param>
        public ApplicationInsightsConfig(IConfigurationSection configurationSection, string envVarName = "APPLICATIONINSIGHTS_CONNECTION_STRING")
        {
            ConnectionString = Environment.GetEnvironmentVariable(envVarName)
               ?? configurationSection.GetValue<string>(nameof(ConnectionString));

            string levelString = configurationSection.GetValue<string>(nameof(MinimumLevel)) ?? "Information";
            MinimumLevel = Enum.TryParse<LogLevel>(levelString, true, out LogLevel parsedLevel) ? parsedLevel : LogLevel.Information;

            EnableAdaptiveSampling = configurationSection.GetValue<bool>(nameof(EnableAdaptiveSampling), false);
        }

        /// <summary>
        /// Returns true if Application Insights is configured (connection string is available).
        /// </summary>
        public bool IsEnabled => !string.IsNullOrEmpty(ConnectionString);
    }
}