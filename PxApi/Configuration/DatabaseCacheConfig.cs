namespace PxApi.Configuration
{
    /// <summary>
    /// Configuration for database caching.
    /// </summary>
    public class DatabaseCacheConfig
    {
        /// <summary>
        /// Configuration for table list caching.
        /// </summary>
        public CacheConfig TableList { get; }

        /// <summary>
        /// Configuration for metadata caching.
        /// </summary>
        public CacheConfig Meta { get; }

        /// <summary>
        /// Configuration for data caching.
        /// </summary>
        public CacheConfig Data { get; }

        /// <summary>
        /// Configuration for modified time caching.
        /// </summary>
        public CacheConfig Modifiedtime { get; }

        /// <summary>
        /// Configuration for grouping metadata caching.
        /// </summary>
        public CacheConfig Groupings { get; }

        /// <summary>
        /// The maximum size of the cache in bytes.
        /// </summary>
        public long MaxCacheSize { get; }

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="section">Configuration section that contains the settings for database caching.</param>
        /// <exception cref="InvalidOperationException">Thrown if the required configuration value is missing.</exception>
        public DatabaseCacheConfig(IConfigurationSection section)
        {
            TableList = new CacheConfig(section.GetSection(nameof(TableList)));
            Meta = new CacheConfig(section.GetSection(nameof(Meta)));
            Data = new CacheConfig(section.GetSection(nameof(Data)));
            Modifiedtime = new CacheConfig(section.GetSection(nameof(Modifiedtime)));
            Groupings = new CacheConfig(section.GetSection(nameof(Groupings)));

            MaxCacheSize = section.GetValue<long?>(nameof(MaxCacheSize))
                ?? throw new InvalidOperationException($"Missing required configuration value: {nameof(MaxCacheSize)}");
        }
    }
}
