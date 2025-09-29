namespace PxApi.Configuration
{
    /// <summary>
    /// Holds configuration for a database.
    /// </summary>
    public class DataBaseConfig
    {
        /// <summary>
        /// The type of the database.
        /// </summary>
        public DataBaseType Type { get; }

        /// <summary>
        /// The unique identifier for the database.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Configuration for database caching.
        /// </summary>
        public DatabaseCacheConfig CacheConfig { get; }

        /// <summary>
        /// Custom configuration properties specific to this database.
        /// </summary>
        public Dictionary<string, string> Custom { get; }

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="section">Section of the application configuration that contains settings for the data source.</param>
        /// <exception cref="InvalidOperationException">Thrown if the required configuration value is missing.</exception>
        public DataBaseConfig(IConfigurationSection section)
        {
            DataBaseType type = section.GetValue<DataBaseType?>(nameof(Type))
                ?? throw new InvalidOperationException($"Missing required configuration value: {nameof(Type)}");
            Type = type;
            
            string id = section.GetValue<string>(nameof(Id))
                ?? throw new InvalidOperationException($"Missing required configuration value: {nameof(Id)}");
            Id = id;
            
            CacheConfig = new DatabaseCacheConfig(section.GetSection(nameof(CacheConfig)));
            
            Dictionary<string, string> customValues = [];
            IConfigurationSection customSection = section.GetSection(nameof(Custom));
            foreach (IConfigurationSection child in customSection.GetChildren())
            {
                string? value = child.Value;
                if (value is not null)
                {
                    customValues.Add(child.Key, value);
                }
            }
            Custom = customValues;
        }
    }
}
