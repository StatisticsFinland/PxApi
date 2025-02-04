namespace PxApi.Configuration
{
    /// <summary>
    /// Configuration for the local file system that is used as a datasource
    /// </summary>
    public class LocalFileSystemConfig
    {
        /// <summary>
        /// The path to the root of the database in the local file system.
        /// </summary>
        public string RootPath { get; }

        /// <summary>
        /// Configuration for the metadata caching.
        /// </summary>
        public CacheConfig MetadataCache { get; }

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="section"> Configuration section that contains the settings for the local file system datasource.</param>
        /// <exception cref="InvalidOperationException">Thrown if the required configuration value is missing.</exception>
        public LocalFileSystemConfig(IConfigurationSection section)
        {
            RootPath = section.GetValue<string>(nameof(RootPath))
                ?? throw new InvalidOperationException($"Missing required configuration value: {nameof(RootPath)}");

            MetadataCache = new CacheConfig(section.GetSection(nameof(MetadataCache)));
        }
    }
}
