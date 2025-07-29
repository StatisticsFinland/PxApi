using Microsoft.Extensions.Caching.Memory;
using Px.Utils.Models.Metadata;
using System.Diagnostics.CodeAnalysis;

namespace PxApi.Caching
{
    /// <summary>
    /// Wraps a data array together with its associated <see cref="MatrixMap"/> for use in memory caching.
    /// Enables coordinated cache management by providing an eviction notification mechanism.
    /// Used by <see cref="DatabaseCache"/> to track and manage cached data entries and their lifecycle.
    /// </summary>
    public class DataCacheContainer<TData>(MatrixMap map, Task<TData[]> data) : CacheContainer
    {
        /// <summary>
        /// Raised when this container is removed from the cache, allowing subscribers to react to eviction.
        /// Used by <see cref="DatabaseCache"/> to update or clean up related cache entries.
        /// </summary>
        public event Action<DataCacheContainer<TData>>? EvictedFromCache;

        /// <summary>
        /// The <see cref="MatrixMap"/> that uniquely identifies the structure and dimensions of the cached data.
        /// </summary>
        public MatrixMap Map { get; init; } = map;

        /// <summary>
        /// The cached data array associated with the <see cref="MatrixMap"/>.
        /// </summary>
        public Task<TData[]> Data { get; init; } = data;

        /// <summary>
        /// Invokes <see cref="EvictedFromCache"/> when the container is evicted from the cache.
        /// </summary>
        [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Required by cache eviction callback signature")]
        public void EvictionCallback(object? key, object? value, EvictionReason reason, object? state)
        {
            EvictedFromCache?.Invoke(this);
        }
    }
}
