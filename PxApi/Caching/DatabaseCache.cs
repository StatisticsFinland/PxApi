using Microsoft.Extensions.Caching.Memory;
using Px.Utils.Models.Metadata;
using Px.Utils.Models.Metadata.ExtensionMethods;
using PxApi.Configuration;
using PxApi.Models;
using System.Collections.Immutable;

namespace PxApi.Caching
{
    /// <summary>
    /// Provides caching operations for matrix metadata and data containers.
    /// </summary>
    public class DatabaseCache(IMemoryCache memoryCache)
    {
        private readonly Dictionary<string, DatabaseCacheConfig> cacheConfigs = 
            AppSettings.Active.DataBases.ToDictionary(
                db => db.Id,
                db => db.CacheConfig
            );

        private const int DEFAULT_DATACELL_SIZE = 5;
        private const int DEFAULT_UPDATE_TASK_SIZE = 16;
        private const int DEFAULT_META_SIZE = 10000;
        private readonly IMemoryCache _cache = memoryCache;

        private const string FILE_LIST_SEED = "9afc7b09";
        private const string LAST_UPDATED_SEED = "553313ea";
        private const string META_SEED = "c4d8ee8f";
        private const string DATA_SEED = "398baf7d";

        /// <summary>
        /// Attempts to retrieve a list of files associated with the specified database.
        /// </summary>
        /// <param name="dataBase">The database for which the file list is being retrieved. Cannot be null.</param>
        /// <param name="files">When this method returns, contains a task that resolves to an immutable list of files if the operation
        /// succeeds;  otherwise, <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if the file list was successfully retrieved from the cache; otherwise, <see
        /// langword="false"/>.</returns>
        public bool TryGetFileList(DataBaseRef dataBase, out Task<ImmutableSortedDictionary<string, PxFileRef>>? files)
        {
            return _cache.TryGetValue(HashCode.Combine(FILE_LIST_SEED, dataBase), out files);
        }

        /// <summary>
        /// Caches a list of files associated with the specified database.
        /// </summary>
        /// <param name="dataBase">The database for which the file list is being set. Cannot be null.</param>
        /// <param name="files">A task that represents the list of files to cache. The task must resolve to an immutable list of <see
        /// cref="PxFileRef"/> objects.</param>
        public void SetFileList(DataBaseRef dataBase, Task<ImmutableSortedDictionary<string, PxFileRef>> files)
        {
            CacheConfig config = cacheConfigs[dataBase.Id].TableList;
            MemoryCacheEntryOptions options = new()
            {
                SlidingExpiration = config.SlidingExpirationSeconds,
                AbsoluteExpirationRelativeToNow = config.AbsoluteExpirationSeconds
            };
            _cache.Set(HashCode.Combine(FILE_LIST_SEED, dataBase), files, options);
        }

        /// <summary>
        /// Attempts to retrieve the last updated timestamp for the specified file from the cache.
        /// </summary>
        /// <param name="file">The file for which the last updated timestamp is being retrieved. Cannot be null.</param>
        /// <param name="lastUpdated">When this method returns, contains a <see cref="Task{DateTime}"/> representing the last updated timestamp 
        /// of the file if the operation succeeds; otherwise, <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if the last updated timestamp was successfully retrieved from the cache;  otherwise,
        /// <see langword="false"/>.</returns>
        public bool TryGetLastUpdated(PxFileRef file, out Task<DateTime>? lastUpdated)
        {
            return _cache.TryGetValue(HashCode.Combine(LAST_UPDATED_SEED, file), out lastUpdated);
        }

        /// <summary>
        /// Updates the cache with the last updated timestamp for the specified file.
        /// </summary>
        /// <param name="file">The file for which the last updated timestamp is being set.</param>
        /// <param name="lastUpdated">A task that resolves to the <see cref="DateTime"/> representing the last updated timestamp.</param>
        public void SetLastUpdated(PxFileRef file, Task<DateTime> lastUpdated)
        {   
            CacheConfig config = cacheConfigs[file.DataBase.Id].Modifiedtime;
            MemoryCacheEntryOptions options = new()
            {
                SlidingExpiration = config.SlidingExpirationSeconds,
                AbsoluteExpirationRelativeToNow = config.AbsoluteExpirationSeconds,
                Size = DEFAULT_UPDATE_TASK_SIZE,
                Priority = CacheItemPriority.Normal,
            };
            _cache.Set(HashCode.Combine(LAST_UPDATED_SEED, file), lastUpdated, options);
        }

        /// <summary>
        /// Attempts to retrieve metadata container for the specified table from the cache.
        /// </summary>
        public bool TryGetMetadata(PxFileRef file, out MetaCacheContainer? metaContainer)
        {
            return _cache.TryGetValue(HashCode.Combine(META_SEED, file), out metaContainer);
        }

        /// <summary>
        /// Stores matrix metadata in the cache and registers an eviction callback for related data cleanup.
        /// </summary>
        public void SetMetadata(PxFileRef file, MetaCacheContainer metaContainer)
        {
            CacheConfig config = cacheConfigs[file.DataBase.Id].Meta;
            MemoryCacheEntryOptions options = new()
            {
                Size = DEFAULT_META_SIZE,
                Priority = CacheItemPriority.Normal,
                SlidingExpiration = config.SlidingExpirationSeconds,
                AbsoluteExpirationRelativeToNow = config.AbsoluteExpirationSeconds
            };
            options.RegisterPostEvictionCallback(OnMetaCacheEvicted);
            _cache.Set(HashCode.Combine(META_SEED, file), metaContainer, options);
        }

        /// <summary>
        /// Attempts to remove metadata associated with the specified file from the cache.
        /// </summary>
        /// <remarks>This method removes the metadata entry for the given file from the cache if it
        /// exists.  If the file is not present in the cache, no action is taken.</remarks>
        /// <param name="file">The file whose associated metadata should be removed.</param>
        public void TryRemoveMeta(PxFileRef file)
        {
            _cache.Remove(HashCode.Combine(META_SEED, file));
        }

        /// <summary>
        /// Attempts to retrieve cached data for the specified matrix map.
        /// </summary>
        public bool TryGetData<TData>(IMatrixMap map, out Task<TData[]>? data, out DateTime? cached)
        {
            if (_cache.TryGetValue(HashCode.Combine(DATA_SEED, MapHash(map)), out DataCacheContainer<TData>? dataContainer))
            {
                data = dataContainer!.Data;
                cached = dataContainer!.CachedUtc;
                return true;
            }
            data = null;
            cached = null;
            return false;
        }

        /// <summary>
        /// Attempts to retrieve a cached superset of the requested data and its corresponding map.
        /// </summary>
        /// <param name="file">The identifier of the file.</param>
        /// <param name="map" >The matrix map for which to find a superset.</param>
        /// <param name="supersetMap">The superset map that contains the requested data.</param>
        /// <param name="data">The data in the superset.</param>
        /// <param name="cached">The timestamp of when the data was cached.</param>
        public bool TryGetDataSuperset<TData>(PxFileRef file, IMatrixMap map, out IMatrixMap? supersetMap, out Task<TData[]>? data, out DateTime? cached)
        {
            if (_cache.TryGetValue(HashCode.Combine(META_SEED, file), out MetaCacheContainer? metaContainer) && metaContainer is not null)
            {
                if(metaContainer.TryGetSuperMap(map, out supersetMap))
                {
                    if (_cache.TryGetValue(HashCode.Combine(DATA_SEED, MapHash(supersetMap)), out DataCacheContainer<TData>? superContainer))
                    {
                        data = superContainer!.Data;
                        cached = superContainer!.CachedUtc;
                        return true;
                    }
                }
            }

            supersetMap = null;
            data = null;
            cached = null;
            return false;
        }

        /// <summary>
        /// Stores data for the specified matrix map in the cache and manages eviction of submaps.
        /// </summary>
        public void SetData<TData>(PxFileRef file, MatrixMap map, Task<TData[]> data)
        {
            if (_cache.TryGetValue(HashCode.Combine(META_SEED, file), out MetaCacheContainer? metaContainer))
            {
                List<IMatrixMap> subMaps = [..metaContainer!.GetSubMaps(map)];
                foreach (IMatrixMap subMap in subMaps)
                { 
                    // Triggers eviction
                    _cache.Remove(HashCode.Combine(DATA_SEED, MapHash(subMap)));
                }

                CacheConfig config = cacheConfigs[file.DataBase.Id].Data;
                MemoryCacheEntryOptions options = new()
                {
                    Size = map.GetSize() * DEFAULT_DATACELL_SIZE,
                    Priority = CacheItemPriority.Low,
                    SlidingExpiration = config.SlidingExpirationSeconds,
                    AbsoluteExpirationRelativeToNow = config.AbsoluteExpirationSeconds
                };

                DataCacheContainer<TData> value = new(map, data);
                options.RegisterPostEvictionCallback(value.EvictionCallback);
                _cache.Set(HashCode.Combine(DATA_SEED, MapHash(map)), value, options);
                metaContainer.AddDataContainer(value);
            }
            else
            {
                throw new InvalidOperationException($"Unable to set data container to cache, related meta container for tableId '{file}' not found.");
            }
        }

        /// <summary>
        /// Clears the file list cache.
        /// </summary>
        public void ClearFileListCache(DataBaseRef dbRef)
        {
            _cache.Remove(HashCode.Combine(FILE_LIST_SEED, dbRef));
        }

        /// <summary>
        /// Clears the metadata cache for the specified files.
        /// </summary>
        /// <param name="fileList">List of files for which to clear metadata cache.</param>
        public void ClearMetadataCache(IEnumerable<PxFileRef> fileList)
        {
            foreach (PxFileRef file in fileList)
            {
                TryRemoveMeta(file);
                _cache.Remove(HashCode.Combine(LAST_UPDATED_SEED, file));
            }
        }

        /// <summary>
        /// Clears the last updated timestamp cache for the specified files.
        /// </summary>
        /// <param name="fileList">List of files for which to clear last updated timestamp cache.</param>
        public void ClearLastUpdatedCache(IEnumerable<PxFileRef> fileList)
        {
            foreach (PxFileRef file in fileList)
            {
                _cache.Remove(HashCode.Combine(LAST_UPDATED_SEED, file));
            }
        }

        /// <summary>
        /// Clears the data cache for the specified files.
        /// </summary>
        /// <param name="fileList">List of files for which to clear data cache.</param>
        public void ClearDataCache(IEnumerable<PxFileRef> fileList)
        {
            foreach (PxFileRef file in fileList)
            {
                if (_cache.TryGetValue(HashCode.Combine(META_SEED, file), out MetaCacheContainer? metaContainer) && metaContainer != null)
                {
                    metaContainer.GetRelatedMaps().ForEach(map =>
                        _cache.Remove(HashCode.Combine(DATA_SEED, MapHash(map))));
                }
            }
        }

        private void OnMetaCacheEvicted(object? key, object? value, EvictionReason reason, object? state)
        {
            if (value is MetaCacheContainer metaContainder)
            {
                metaContainder.GetRelatedMaps().ForEach(map =>
                    _cache.Remove(HashCode.Combine(DATA_SEED, MapHash(map))));
            }
        }

        private static int MapHash(IMatrixMap map)
        {
            HashCode hash = new();
            foreach (IDimensionMap item in map.DimensionMaps)
            {
                hash.Add(item.Code);
                foreach(string valCode in item.ValueCodes)
                {
                    hash.Add(valCode);
                }
            }
            return hash.ToHashCode();
        }
    }
}
