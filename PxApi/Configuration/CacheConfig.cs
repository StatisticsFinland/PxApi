namespace PxApi.Configuration
{
    /// <summary>
    /// Caching related configuration.
    /// </summary>
    public class CacheConfig
    {
        /// <summary>
        /// If the cache entry is not accessed for this amount of time, it will be removed from the cache.
        /// </summary>
        public TimeSpan SlidingExpirationMinutes { get; }

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="section"> Configuration section that contains the settings for caching.</param>
        /// <exception cref="InvalidOperationException">Thrown if the required configuration value is missing.</exception>
        public CacheConfig(IConfigurationSection section)
        {
            int minutes = section.GetValue<int?>(nameof(SlidingExpirationMinutes))
                ?? throw new InvalidOperationException($"Missing required configuration value: {nameof(SlidingExpirationMinutes)}");

            SlidingExpirationMinutes = TimeSpan.FromMinutes(minutes);
        }
    }
}
