namespace PxApi.Caching
{
    /// <summary>
    /// Base class for cache containers.
    /// </summary>
    public abstract class CacheContainer
    {
        /// <summary>
        /// Stores the UTC time when this cache container was created.
        /// </summary>
        public DateTime CachedUtc { get; } = DateTime.UtcNow;
    }
}
