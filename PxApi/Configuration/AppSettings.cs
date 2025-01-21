namespace PxApi.Configuration
{
    public class AppSettings
    {
        public DataSourceConfig DataSource { get; }

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
            RootUrl = new Uri(rootStr, UriKind.Absolute)
                ?? throw new InvalidOperationException("RootUrl is not valid absolute url");
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
