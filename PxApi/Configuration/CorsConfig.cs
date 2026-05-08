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
        /// <exception cref="InvalidOperationException">Thrown when an origin value is not a valid absolute http or https URI.</exception>
        internal CorsConfig(IConfigurationSection section)
        {
            string[] rawOrigins = section.GetSection(nameof(AllowedOrigins)).Get<string[]>() ?? [];

            List<string> validatedOrigins = rawOrigins
                .Where(o => !string.IsNullOrWhiteSpace(o))
                .Select(o => o.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (string origin in validatedOrigins)
            {
                if (!Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    throw new InvalidOperationException(
                        $"Invalid CORS origin '{origin}' in configuration. Each origin must be an absolute http or https URI (e.g., 'https://example.com').");
                }
            }

            AllowedOrigins = validatedOrigins;
        }
    }
}
