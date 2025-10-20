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
        public TimeSpan SlidingExpirationSeconds { get; }

        /// <summary>
        /// The absolute maximum age of the cache entry.
        /// </summary>
        public TimeSpan AbsoluteExpirationSeconds { get; }

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="section"> Configuration section that contains the settings for caching.</param>
        /// <exception cref="InvalidOperationException">Thrown if the required configuration value is missing.</exception>
        public CacheConfig(IConfigurationSection section)
        {
            int slidingSeconds = section.GetValue<int?>(nameof(SlidingExpirationSeconds))
                ?? throw new InvalidOperationException($"Missing required configuration value: {nameof(SlidingExpirationSeconds)}");

            SlidingExpirationSeconds = TimeSpan.FromSeconds(slidingSeconds);

            int absoluteSeconds = section.GetValue<int?>(nameof(AbsoluteExpirationSeconds))
                ?? throw new InvalidOperationException($"Missing required configuration value: {nameof(AbsoluteExpirationSeconds)}");

            AbsoluteExpirationSeconds = TimeSpan.FromSeconds(absoluteSeconds);
        }
    }
}
