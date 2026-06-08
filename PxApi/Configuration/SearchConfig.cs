namespace PxApi.Configuration
{
    /// <summary>
    /// Configuration for Elasticsearch search backend.
    /// </summary>
    public class SearchConfig(IConfigurationSection configuration)
    {
        /// <summary>
        /// Elastic Cloud deployment identifier.
        /// </summary>
        public string CloudId { get; } = configuration.GetValue<string>(nameof(CloudId)) ?? string.Empty;

        /// <summary>
        /// API key for authenticating with the Elasticsearch cluster.
        /// This value is sourced from the <c>SEARCH_API_KEY</c> environment variable and is not read from the provided configuration section.
        /// </summary>
        public string ApiKey { get; } = Environment.GetEnvironmentVariable("SEARCH_API_KEY") ?? string.Empty;

        /// <summary>
        /// Index name prefix. The language code is appended as a suffix (e.g. <c>my-index-fi</c>).
        /// </summary>
        public string IndexPrefix { get; } = configuration.GetValue<string>(nameof(IndexPrefix)) ?? string.Empty;

        /// <summary>
        /// Optional HTTP proxy address for connecting to the Elasticsearch cluster (e.g. <c>http://proxy.example.com:8080</c>).
        /// When omitted or empty, no proxy is used.
        /// </summary>
        public string? ProxyAddress { get; } = configuration.GetValue<string>(nameof(ProxyAddress));
    }
}
