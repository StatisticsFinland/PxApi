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
