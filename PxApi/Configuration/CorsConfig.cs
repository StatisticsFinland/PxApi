namespace PxApi.Configuration
{
    /// <summary>
    /// Configuration for Cross-Origin Resource Sharing (CORS) settings.
    /// </summary>
    public class CorsConfig
    {
        /// <summary>
        /// Gets the list of allowed origins for CORS requests.
        /// When empty, no custom CORS policy is applied.
        /// </summary>
        public IReadOnlyList<string> AllowedOrigins { get; }

        /// <summary>
        /// Gets a value indicating whether any custom CORS origins are configured.
        /// </summary>
        public bool HasAllowedOrigins => AllowedOrigins.Count > 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="CorsConfig"/> class from configuration.
        /// </summary>
        /// <param name="section">The configuration section representing the CORS settings.</param>
        internal CorsConfig(IConfigurationSection section)
        {
            AllowedOrigins = section.GetSection(nameof(AllowedOrigins)).Get<string[]>() ?? [];
        }
    }
}
