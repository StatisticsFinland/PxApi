namespace PxApi.Configuration
{
    /// <summary>
    /// The main class for all application settings.
    /// Use the <see cref="Load"/> method to load the settings from the configuration.
    /// The loaded settings can be accessed through the <see cref="Active"/> property.
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// The configuration for each database.
        /// </summary>
        public List<DataBaseConfig> DataBases { get; }

        /// <summary>
        /// The root URL where the application is hosted.
        /// Used to create URLs for the API.
        /// </summary>
        public Uri RootUrl { get; }

        /// <summary>
        /// Feature flags configuration for controlling application behavior.
        /// </summary>
        public FeatureFlagsConfig Features { get; }

        /// <summary>
        /// Authentication configuration for controlling access to protected endpoints.
        /// </summary>
        public AuthenticationConfig Authentication { get; }

        /// <summary>
        /// Query limits configuration for controlling maximum request sizes.
        /// </summary>
        public QueryLimitsConfig QueryLimits { get; }

        /// <summary>
        /// Global cache configuration for controlling cache behavior.
        /// </summary>
        public MemoryCacheConfig Cache { get; }

        /// <summary>
        /// OpenAPI related configuration (contact and license information).
        /// </summary>
        public OpenApiConfig OpenApi { get; }

        /// <summary>
        /// Localization configuration containing default language and supported languages.
        /// </summary>
        public LocalizationConfig Localization { get; }

        /// <summary>
        /// Optional configuration for blob read mode selection thresholds.
        /// Default values are used when the configuration section is not present.
        /// </summary>
        public BlobReadModeConfig BlobReadMode { get; }

        /// <summary>
        /// Application Insights configuration for telemetry and logging.
        /// </summary>
        public ApplicationInsightsConfig ApplicationInsights { get; }

        /// <summary>
        /// The currently active configuration for the application.
        /// </summary>
        public static AppSettings Active
        { 
            get
            {
                if (_active is null)
                {
                    string eMsg = $"AppSettings has not been loaded. Call {nameof(Load)} before accessing the settings.";
                    throw new InvalidOperationException(eMsg);
                }
                return _active;
            }
        }

        private static AppSettings? _active;

        /// <summary>
        /// Private constructor that initializes the AppSettings from the provided configuration.
        /// </summary>
        /// <param name="configuration">The configuration to read settings from.</param>
        /// <exception cref="InvalidOperationException">Thrown if required configuration values are missing.</exception>
        private AppSettings(IConfiguration configuration)
        {
            string rootUrlString = configuration.GetValue<string>(nameof(RootUrl)) 
                ?? throw new InvalidOperationException($"Missing required configuration value: {nameof(RootUrl)}");
            RootUrl = new Uri(rootUrlString, UriKind.Absolute);

            List<DataBaseConfig> databases = [];
            IConfigurationSection databasesSection = configuration.GetSection(nameof(DataBases));
            foreach (IConfigurationSection databaseSection in databasesSection.GetChildren())
            {
                DataBaseConfig databaseConfig = new(databaseSection);
                databases.Add(databaseConfig);
            }
            DataBases = databases;

            Features = new FeatureFlagsConfig(configuration.GetSection("FeatureManagement"));
            Authentication = new AuthenticationConfig(configuration.GetSection(nameof(Authentication)));
            QueryLimits = new QueryLimitsConfig(configuration.GetSection(nameof(QueryLimits)));
            Cache = new MemoryCacheConfig(configuration.GetSection(nameof(Cache)));
            OpenApi = new OpenApiConfig(configuration.GetSection(nameof(OpenApi)));
            Localization = new LocalizationConfig(configuration.GetSection(nameof(Localization)));
            ApplicationInsights = new ApplicationInsightsConfig(configuration.GetSection(nameof(ApplicationInsights)));
            BlobReadMode = new BlobReadModeConfig(configuration.GetSection(nameof(BlobReadMode)));
        }

        /// <summary>
        /// Load the AppSettings from the provided configuration.
        /// The loaded settings can be accessed through the <see cref="Active"/> property.
        /// </summary>
        /// <param name="configuration">The configuration to load settings from.</param>
        public static void Load(IConfiguration configuration)
        {
            _active = new AppSettings(configuration);
        }
    }
}
