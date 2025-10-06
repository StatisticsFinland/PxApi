namespace PxApi.Configuration
{
    /// <summary>
    /// Configuration for feature flags that control endpoint availability.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="FeatureFlagsConfig"/> class.
    /// </remarks>
    /// <param name="configuration">The configuration section containing feature flag settings.</param>
    public class FeatureFlagsConfig(IConfigurationSection configuration)
    {
        /// <summary>
        /// Determines whether the cache controller endpoints are enabled.
        /// When false, all cache management endpoints will return 404 Not Found.
        /// Note: Cache endpoints are always hidden from OpenAPI documentation since they are for internal use only.
        /// Defaults to false since cache endpoints are for internal use only.
        /// </summary>
        public bool CacheController { get; } = configuration.GetValue<bool>(nameof(CacheController), defaultValue: false);
    }
}