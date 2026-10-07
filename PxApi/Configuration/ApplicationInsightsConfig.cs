namespace PxApi.Configuration
{
    /// <summary>
    /// Configuration for Application Insights integration.
    /// Connection string can be provided via configuration or the APPLICATIONINSIGHTS_CONNECTION_STRING environment variable.
    /// SDK 3 log level filtering uses the standard <c>Logging:OpenTelemetry:LogLevel</c> configuration section.
    /// </summary>
    public class ApplicationInsightsConfig
    {
        /// <summary>
        /// Application Insights connection string.
        /// Can be overridden by the APPLICATIONINSIGHTS_CONNECTION_STRING environment variable.
        /// </summary>
        public string? ConnectionString { get; }

        /// <summary>SDK 3.x trace sampling ratio, between zero and one. Defaults to one.</summary>
        public float SamplingRatio { get; }

        /// <summary>Opt-in to sample logs using their trace's sampling decision.</summary>
        public bool EnableTraceBasedLogsSampler { get; }

        /// <summary>Explicit privacy opt-in to send security audit records to Application Insights.</summary>
        public bool IncludeAuditLogs { get; }

        /// <summary>
        /// Initializes ApplicationInsights configuration from the provided configuration section.
        /// </summary>
        /// <param name="configurationSection">Configuration section containing ApplicationInsights settings.</param>
        /// <param name="envVarName">Optional environment variable key for the connection string, default is "APPLICATIONINSIGHTS_CONNECTION_STRING".</param>
        public ApplicationInsightsConfig(IConfigurationSection configurationSection, string envVarName = "APPLICATIONINSIGHTS_CONNECTION_STRING")
        {
            ConnectionString = Environment.GetEnvironmentVariable(envVarName)
               ?? configurationSection.GetValue<string>(nameof(ConnectionString));
                float ratio = configurationSection.GetValue(nameof(SamplingRatio), 1f);
                SamplingRatio = float.IsFinite(ratio) && ratio is >= 0 and <= 1 ? ratio : 1f;
                EnableTraceBasedLogsSampler = configurationSection.GetValue(nameof(EnableTraceBasedLogsSampler), false);
                IncludeAuditLogs = configurationSection.GetValue(nameof(IncludeAuditLogs), false);
        }

        /// <summary>
        /// Returns true if Application Insights is configured (connection string is available).
        /// </summary>
        public bool IsEnabled => !string.IsNullOrEmpty(ConnectionString);
    }
}