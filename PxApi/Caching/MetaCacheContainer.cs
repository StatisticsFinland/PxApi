using Px.Utils.Models.Metadata;
using Px.Utils.Models.Metadata.ExtensionMethods;
using System.Diagnostics.CodeAnalysis;

namespace PxApi.Caching
{
    /// <summary>
    /// Container for matrix metadata with tracking of related cached data containers.
    /// Provides coordination between metadata and its associated data containers in the cache.
    /// </summary>
    public class MetaCacheContainer(Task<IReadOnlyMatrixMetadata> meta) : CacheContainer
    {
        /// <summary>
        /// Gets the metadata for the matrix.
        /// </summary>
        public Task<IReadOnlyMatrixMetadata> Metadata { get; private init; } = meta;

        internal object Generation { get; } = new();

        internal Lock DataAccessLock { get; } = new();

        // The non-generic base allows keys for DataCacheContainer<TData> instances of any data type.
        // Track by container identity so delayed eviction cannot unregister a replacement with the same map.
        private readonly Dictionary<CacheContainer, IMatrixMap> _relatedCachedData = new(ReferenceEqualityComparer.Instance);

        /// <summary>
        /// Checks if the collection of related maps contains a map identical to the provided map.
        /// </summary>
        /// <param name="map">The matrix map to compare against related maps.</param>
        /// <returns>True if an identical map exists in the related maps; otherwise, false.</returns>
        public bool HasIdenticalMap(IMatrixMap map)
        {
            lock (DataAccessLock)
            {
                return _relatedCachedData.Values.Any(m => m.IsIdenticalMapTo(map));
            }
        }

        /// <summary>
        /// Attempts to find a superset map of the provided map within the related maps.
        /// </summary>
        /// <param name="map">The matrix map to find a superset for.</param>
        /// <param name="super">When this method returns, contains the superset map if found; otherwise, null.</param>
        /// <returns>True if a superset map was found; otherwise, false.</returns>
        public bool TryGetSuperMap(IMatrixMap map, [NotNullWhen(true)] out IMatrixMap? super)
        {
            lock (DataAccessLock)
            {
                foreach (IMatrixMap related in _relatedCachedData.Values)
                {
                    if (related.IsSupermapOf(map))
                    {
                        super = related;
                        return true;
                    }
                }
                super = null;
                return false;
            }
        }

        /// <summary>
        /// Gets a snapshot of all related maps that are submaps of the provided map.
        /// </summary>
        /// <param name="map">The matrix map to find submaps for.</param>
        /// <returns>A collection of matrix maps that are submaps of the provided map.</returns>
        public IEnumerable<IMatrixMap> GetSubMaps(IMatrixMap map)
        {
            lock (DataAccessLock)
            {
                return [.._relatedCachedData.Values.Where(m => m.IsSubmapOf(map))];
            }
        }

        /// <summary>
        /// Gets a copy of all related matrix maps tracked by this container.
        /// </summary>
        /// <returns>A list containing copies of all related matrix maps.</returns>
        public List<IMatrixMap> GetRelatedMaps()
        {
            lock (DataAccessLock)
            {
                // Return a shallow copy to avoid external modifications or read/write conflicts
                return [.. _relatedCachedData.Values];
            }
        }

        /// <summary>
        /// Adds a data container to the related maps and subscribes to its eviction event.
        /// </summary>
        /// <typeparam name="TData">The type of data stored in the container.</typeparam>
        /// <param name="container">The data container to track.</param>
        public void AddDataContainer<TData>(DataCacheContainer<TData> container)
        {
            lock (DataAccessLock)
            {
                if (_relatedCachedData.TryAdd(container, container.Map))
                {
                    container.EvictedFromCache += TryRemoveDataContainer;
                }
            }
        }

        /// <summary>
        /// Removes a data container's map from the related maps and unsubscribes from its eviction event.
        /// </summary>
        /// <typeparam name="TData">The type of data stored in the container.</typeparam>
        /// <param name="container">The data container to remove.</param>
        public void TryRemoveDataContainer<TData>(DataCacheContainer<TData> container)
        {
            lock (DataAccessLock)
            {
                container.EvictedFromCache -= TryRemoveDataContainer;
                _relatedCachedData.Remove(container);
            }
        }
    }
}
