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
        /// The configuration for the data source.
        /// </summary>
        public DataSourceConfig DataSource { get; }

        /// <summary>
        /// The root URL where the application is hosted.
        /// Used to create URLs for the API.
        /// </summary>
        public Uri RootUrl { get; }

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

        private AppSettings(IConfiguration dataSourceConfig)
        {
            IConfigurationSection section = dataSourceConfig.GetRequiredSection(nameof(DataSource));
            DataSource = new DataSourceConfig(section);
            string rootStr = dataSourceConfig.GetValue<string>(nameof(RootUrl))
                ?? throw new InvalidOperationException("RootUrl is not set in the configuration.");
            RootUrl = new Uri(rootStr, UriKind.Absolute);
        }

        /// <summary>
        /// Load the AppSettings from the provided configuration.
        /// The loaded settings can be accessed through the <see cref="Active"/> property.
        /// </summary>
        /// <param name="configuration"></param>
        public static void Load(IConfiguration configuration)
        {
            _active = new AppSettings(configuration);
        }
    }
}
