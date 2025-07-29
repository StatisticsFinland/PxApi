namespace PxApi.Caching
{
    public abstract class CacheContainer
    {
        public DateTime CachedUtc { get; } = DateTime.UtcNow;
    }
}
