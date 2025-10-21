namespace PxApi.Configuration
{
    /// <summary>
    /// Configuration for authentication settings.
    /// </summary>
    public class AuthenticationConfig
    {
        /// <summary>
        /// Configuration for cache controller API key authentication.
        /// </summary>
        public CacheApiKeyConfig Cache { get; }
        
        /// <summary>
        /// Gets a value indicating whether any authentication method is enabled.
        /// </summary>
        public bool IsEnabled => Cache.IsEnabled;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticationConfig"/> class.
        /// </summary>
        /// <param name="configuration">The configuration section containing authentication settings.</param>
        public AuthenticationConfig(IConfigurationSection configuration)
        {
            Cache = new CacheApiKeyConfig(configuration.GetSection(nameof(Cache)));
        }
    }
    
    /// <summary>
    /// Abstract base class for API key authentication configuration.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ApiKeyConfig"/> class.
    /// </remarks>
    /// <param name="configuration">The configuration section containing API key settings.</param>
    public abstract class ApiKeyConfig(IConfigurationSection configuration)
    {
        /// <summary>
        /// The hashed API key value for comparison with client-provided keys.
        /// If not provided, API key authentication is disabled.
        /// </summary>
        public string? Hash { get; protected set; } = configuration.GetValue<string>(nameof(Hash));
        
        /// <summary>
        /// The salt value used for hashing API keys.
        /// Required when Hash is provided.
        /// </summary>
        public string? Salt { get; protected set; } = configuration.GetValue<string>(nameof(Salt));
        
        /// <summary>
        /// The name of the HTTP header that should contain the API key.
        /// Defaults to the implementation-specific default if not specified.
        /// </summary>
        public string HeaderName { get; protected set; } = string.Empty;
        
        /// <summary>
        /// Gets a value indicating whether API key authentication is enabled.
        /// Enabled when both Hash and Salt are provided.
        /// </summary>
        public bool IsEnabled => !string.IsNullOrEmpty(Hash) && !string.IsNullOrEmpty(Salt);
    }
    
    /// <summary>
    /// Configuration for cache controller API key authentication.
    /// </summary>
    public class CacheApiKeyConfig : ApiKeyConfig
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CacheApiKeyConfig"/> class.
        /// </summary>
        /// <param name="configuration">The configuration section containing cache API key settings.</param>
        public CacheApiKeyConfig(IConfigurationSection configuration) : base(configuration)
        {
            Hash = configuration.GetValue<string>(nameof(Hash));
            Salt = configuration.GetValue<string>(nameof(Salt));
            HeaderName = configuration.GetValue<string>(nameof(HeaderName)) ?? DEFAULT_HEADER_NAME;
        }

        /// <summary>
        /// Gets the default header name for cache controller authentication.
        /// </summary>
        /// <returns>The default header name "X-Cache-API-Key".</returns>
        private const string DEFAULT_HEADER_NAME = "X-Cache-API-Key";
    }
}