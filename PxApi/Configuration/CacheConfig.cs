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
        /// The absolute maximum age of the cache entry.
        /// </summary>
        public TimeSpan AbsoluteExpirationMinutes { get; }

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="section"> Configuration section that contains the settings for caching.</param>
        /// <exception cref="InvalidOperationException">Thrown if the required configuration value is missing.</exception>
        public CacheConfig(IConfigurationSection section)
        {
            int slidingMinutes = section.GetValue<int?>(nameof(SlidingExpirationMinutes))
                ?? throw new InvalidOperationException($"Missing required configuration value: {nameof(SlidingExpirationMinutes)}");

            SlidingExpirationMinutes = TimeSpan.FromMinutes(slidingMinutes);

            int absoluteMinutes = section.GetValue<int?>(nameof(AbsoluteExpirationMinutes))
                ?? throw new InvalidOperationException($"Missing required configuration value: {nameof(AbsoluteExpirationMinutes)}");

            AbsoluteExpirationMinutes = TimeSpan.FromMinutes(absoluteMinutes);
        }
    }
}
