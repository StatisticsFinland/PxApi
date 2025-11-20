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
        /// Configuration for databases controller API key authentication.
        /// </summary>
        public DatabasesApiKeyConfig Databases { get; }

        /// <summary>
        /// Configuration for tables controller API key authentication.
        /// </summary>
        public TablesApiKeyConfig Tables { get; }

        /// <summary>
        /// Configuration for metadata controller API key authentication.
        /// </summary>
        public MetadataApiKeyConfig Metadata { get; }

        /// <summary>
        /// Configuration for data controller API key authentication.
        /// </summary>
        public DataApiKeyConfig Data { get; }

        /// <summary>
        /// Gets a value indicating whether any authentication method is enabled.
        /// </summary>
        public bool IsEnabled => Cache.IsEnabled || Databases.IsEnabled || Tables.IsEnabled || Metadata.IsEnabled || Data.IsEnabled;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticationConfig"/> class.
        /// </summary>
        /// <param name="configuration">The configuration section containing authentication settings.</param>
        public AuthenticationConfig(IConfigurationSection configuration)
        {
            Cache = new CacheApiKeyConfig(configuration.GetSection(nameof(Cache)));
            Databases = new DatabasesApiKeyConfig(configuration.GetSection(nameof(Databases)));
            Tables = new TablesApiKeyConfig(configuration.GetSection(nameof(Tables)));
            Metadata = new MetadataApiKeyConfig(configuration.GetSection(nameof(Metadata)));
            Data = new DataApiKeyConfig(configuration.GetSection(nameof(Data)));
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
        /// Gets the default header name for cache controller authentication.
        /// </summary>
        private const string DEFAULT_HEADER_NAME = "X-Cache-API-Key";

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
    }

    /// <summary>
    /// Configuration for databases controller API key authentication.
    /// </summary>
    public class DatabasesApiKeyConfig : ApiKeyConfig
    {
        /// <summary>
        /// Gets the default header name for databases controller authentication.
        /// </summary>
        private const string DEFAULT_HEADER_NAME = "X-Databases-API-Key";

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabasesApiKeyConfig"/> class.
        /// </summary>
        /// <param name="configuration">The configuration section containing databases API key settings.</param>
        public DatabasesApiKeyConfig(IConfigurationSection configuration) : base(configuration)
        {
            Hash = configuration.GetValue<string>(nameof(Hash));
            Salt = configuration.GetValue<string>(nameof(Salt));
            HeaderName = configuration.GetValue<string>(nameof(HeaderName)) ?? DEFAULT_HEADER_NAME;
        }
    }

    /// <summary>
    /// Configuration for tables controller API key authentication.
    /// </summary>
    public class TablesApiKeyConfig : ApiKeyConfig
    {
        /// <summary>
        /// Gets the default header name for tables controller authentication.
        /// </summary>
        private const string DEFAULT_HEADER_NAME = "X-Tables-API-Key";

        /// <summary>
        /// Initializes a new instance of the <see cref="TablesApiKeyConfig"/> class.
        /// </summary>
        /// <param name="configuration">The configuration section containing tables API key settings.</param>
        public TablesApiKeyConfig(IConfigurationSection configuration) : base(configuration)
        {
            Hash = configuration.GetValue<string>(nameof(Hash));
            Salt = configuration.GetValue<string>(nameof(Salt));
            HeaderName = configuration.GetValue<string>(nameof(HeaderName)) ?? DEFAULT_HEADER_NAME;
        }
    }

    /// <summary>
    /// Configuration for metadata controller API key authentication.
    /// </summary>
    public class MetadataApiKeyConfig : ApiKeyConfig
    {
        /// <summary>
        /// Gets the default header name for metadata controller authentication.
        /// </summary>
        private const string DEFAULT_HEADER_NAME = "X-Metadata-API-Key";

        /// <summary>
        /// Initializes a new instance of the <see cref="MetadataApiKeyConfig"/> class.
        /// </summary>
        /// <param name="configuration">The configuration section containing metadata API key settings.</param>
        public MetadataApiKeyConfig(IConfigurationSection configuration) : base(configuration)
        {
            Hash = configuration.GetValue<string>(nameof(Hash));
            Salt = configuration.GetValue<string>(nameof(Salt));
            HeaderName = configuration.GetValue<string>(nameof(HeaderName)) ?? DEFAULT_HEADER_NAME;
        }
    }

    /// <summary>
    /// Configuration for data controller API key authentication.
    /// </summary>
    public class DataApiKeyConfig : ApiKeyConfig
    {
        /// <summary>
        /// Gets the default header name for data controller authentication.
        /// </summary>
        private const string DEFAULT_HEADER_NAME = "X-Data-API-Key";

        /// <summary>
        /// Initializes a new instance of the <see cref="DataApiKeyConfig"/> class.
        /// </summary>
        /// <param name="configuration">The configuration section containing data API key settings.</param>
        public DataApiKeyConfig(IConfigurationSection configuration) : base(configuration)
        {
            Hash = configuration.GetValue<string>(nameof(Hash));
            Salt = configuration.GetValue<string>(nameof(Salt));
            HeaderName = configuration.GetValue<string>(nameof(HeaderName)) ?? DEFAULT_HEADER_NAME;
        }
    }
}