using Microsoft.Extensions.Caching.Memory;
using Px.Utils.Language;
using Px.Utils.Models.Metadata.ExtensionMethods;
using Px.Utils.Models.Metadata;
using PxApi.Configuration;
using PxApi.Models;
using System.Collections.Immutable;

namespace PxApi.Caching
{
    /// <summary>
    /// Provides caching operations for matrix metadata and data containers, reporting skipped writes through an optional observer.
    /// </summary>
    public class DatabaseCache(IMemoryCache memoryCache, ICacheObserver? cacheObserver = null)
    {
        private readonly Dictionary<string, DatabaseCacheConfig> cacheConfigs = 
            AppSettings.Active.DataBases.ToDictionary(
                db => db.Id,
                db => db.CacheConfig
            );

        private readonly MemoryCacheConfig memoryCacheConfig = AppSettings.Active.Cache;
        private readonly IMemoryCache _cache = memoryCache;

        private const string FILE_LIST_SEED = "9afc7b09";
        private const string LAST_UPDATED_SEED = "553313ea";
        private const string META_SEED = "c4d8ee8f";
        private const string DATA_SEED = "398baf7d";
        private const string DATABASE_NAME_SEED = "1e4c2a77";

        /// <summary>
        /// Attempts to retrieve a list of files associated with the specified database.
        /// </summary>
        /// <param name="dataBase">The database for which the file list is being retrieved. Cannot be null.</param>
        /// <param name="files">When this method returns, contains a task that resolves to an immutable list of files if the operation
        /// succeeds; otherwise, <see langword="null"/>.</param>
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
                AbsoluteExpirationRelativeToNow = config.AbsoluteExpirationSeconds,
                Priority = CacheItemPriority.High,
                Size = memoryCacheConfig.DefaultFileListSize
            };
            int cacheKey = HashCode.Combine(FILE_LIST_SEED, dataBase);
            _cache.Set(cacheKey, files, options);
            AttachEvictionOnFail(files, cacheKey);
        }

        /// <summary>
        /// Attempts to retrieve a cached multilanguage database name (from Alias_*.txt files) for the specified database.
        /// </summary>
        /// <param name="dataBase">Database reference whose name is requested.</param>
        /// <param name="name">When this method returns contains the cached multilanguage database name task if found.</param>
        /// <returns>True if found, otherwise false.</returns>
        public bool TryGetDatabaseName(DataBaseRef dataBase, out Task<MultilanguageString>? name)
        {
            return _cache.TryGetValue(HashCode.Combine(DATABASE_NAME_SEED, dataBase), out name);
        }

        /// <summary>
        /// Caches a multilanguage database name (built from Alias_*.txt files) for the specified database.
        /// </summary>
        /// <param name="dataBase">Database reference whose name is being cached.</param>
        /// <param name="name">Task producing the multilanguage database name.</param>
        public void SetDatabaseName(DataBaseRef dataBase, Task<MultilanguageString> name)
        {
            CacheConfig config = cacheConfigs[dataBase.Id].Meta;
            MemoryCacheEntryOptions options = new()
            {
                SlidingExpiration = config.SlidingExpirationSeconds,
                AbsoluteExpirationRelativeToNow = config.AbsoluteExpirationSeconds,
                Priority = CacheItemPriority.Normal,
                Size = memoryCacheConfig.DefaultAliasSize
            };
            int cacheKey = HashCode.Combine(DATABASE_NAME_SEED, dataBase);
            _cache.Set(cacheKey, name, options);
            AttachEvictionOnFail(name, cacheKey);
        }

        /// <summary>
        /// Attempts to retrieve the last updated timestamp for the specified file from the cache.
        /// </summary>
        /// <param name="file">The file for which the last updated timestamp is being retrieved. Cannot be null.</param>
        /// <param name="lastUpdated">When this method returns, contains a <see cref="Task{DateTime}"/> representing the last updated timestamp 
        /// of the file if the operation succeeds; otherwise, <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if the last updated timestamp was successfully retrieved from the cache; otherwise,
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
            int revalidationIntervalMs = cacheConfigs[file.DataBase.Id].RevalidationIntervalMs
                ?? throw new InvalidOperationException($"Trying to cache revalidation information for database {file.DataBase.Id} when it should be disabled.");
            MemoryCacheEntryOptions options = new()
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(revalidationIntervalMs),
                Size = memoryCacheConfig.DefaultUpdateTaskSize,
                Priority = CacheItemPriority.Normal,
            };
            int cacheKey = HashCode.Combine(LAST_UPDATED_SEED, file);
            _cache.Set(cacheKey, lastUpdated, options);
            AttachEvictionOnFail(lastUpdated, cacheKey);
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
                Size = memoryCacheConfig.DefaultMetaSize,
                Priority = CacheItemPriority.Normal,
                SlidingExpiration = config.SlidingExpirationSeconds,
                AbsoluteExpirationRelativeToNow = config.AbsoluteExpirationSeconds
            };

            int cacheKey = HashCode.Combine(META_SEED, file);
            options.RegisterPostEvictionCallback(OnMetaCacheEvicted, file);

            _cache.Set(cacheKey, metaContainer, options);
            AttachEvictionOnFail(metaContainer, metaContainer.Metadata, cacheKey);
        }

        /// <summary>
        /// Attempts to remove metadata associated with the specified file from the cache.
        /// </summary>
        /// <remarks>This method removes the metadata entry for the given file from the cache if it
        /// exists. If the file is not present in the cache, no action is taken.</remarks>
        /// <param name="file">The file whose associated metadata should be removed.</param>
        public void TryRemoveMeta(PxFileRef file)
        {
            _cache.Remove(HashCode.Combine(META_SEED, file));
        }

        /// <summary>
        /// Attempts to retrieve cached data for the specified file and matrix map.
        /// </summary>
        public bool TryGetData<TData>(PxFileRef file, IMatrixMap map, out Task<TData[]>? data, out DateTime? cached)
        {
            if (TryGetMetadata(file, out MetaCacheContainer? metaContainer) &&
                metaContainer is not null &&
                _cache.TryGetValue(GetDataCacheKey(file, metaContainer, map), out DataCacheContainer<TData>? dataContainer) &&
                TryGetMetadata(file, out MetaCacheContainer? currentMetadata) &&
                ReferenceEquals(currentMetadata, metaContainer))
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
            if (_cache.TryGetValue(HashCode.Combine(META_SEED, file), out MetaCacheContainer? metaContainer) &&
                metaContainer is not null &&
                metaContainer.TryGetSuperMap(map, out supersetMap) &&
                _cache.TryGetValue(GetDataCacheKey(file, metaContainer, supersetMap), out DataCacheContainer<TData>? superContainer) &&
                TryGetMetadata(file, out MetaCacheContainer? currentMetadata) &&
                ReferenceEquals(currentMetadata, metaContainer))
            {
                data = superContainer!.Data;
                cached = superContainer!.CachedUtc;
                return true;
            }

            supersetMap = null;
            data = null;
            cached = null;
            return false;
        }

        /// <summary>
        /// Stores data for the specified matrix map in the cache and manages eviction of submaps.
        /// Skips caching and notifies the observer if the related metadata entry was cleared, evicted, or replaced during processing.
        /// </summary>
        /// <param name="file">The file whose data is being cached.</param>
        /// <param name="expectedMetaContainer">The metadata container used to read the data. It must still be the current cached container.</param>
        /// <param name="map">The matrix map describing the data selection.</param>
        /// <param name="data">The task producing the selected data.</param>
        public void SetData<TData>(PxFileRef file, MetaCacheContainer expectedMetaContainer, MatrixMap map, Task<TData[]> data)
        {
            lock (expectedMetaContainer.DataAccessLock)
            {
                if (!TryGetMetadata(file, out MetaCacheContainer? metaContainer) ||
                    metaContainer is null ||
                    !ReferenceEquals(metaContainer, expectedMetaContainer))
                {
                    cacheObserver?.RecordDataCacheWriteSkipped();
                    return;
                }

                List<IMatrixMap> subMaps = [..metaContainer.GetSubMaps(map)];
                foreach (IMatrixMap subMap in subMaps)
                { 
                    // Triggers eviction
                    _cache.Remove(GetDataCacheKey(file, metaContainer, subMap));
                }

                CacheConfig config = cacheConfigs[file.DataBase.Id].Data;
                MemoryCacheEntryOptions options = new()
                {
                    Size = map.GetSize() * memoryCacheConfig.DefaultDataCellSize,
                    Priority = CacheItemPriority.Low,
                    SlidingExpiration = config.SlidingExpirationSeconds,
                    AbsoluteExpirationRelativeToNow = config.AbsoluteExpirationSeconds
                };

                DataCacheContainer<TData> value = new(map, data);
                options.RegisterPostEvictionCallback(value.EvictionCallback);
                (string, PxFileRef, object, int) cacheKey = GetDataCacheKey(file, metaContainer, map);
                // Register before publishing so even immediate eviction can remove this container's registration.
                metaContainer.AddDataContainer(value);
                _cache.Set(cacheKey, value, options);
                AttachEvictionOnFail(value, value.Data, cacheKey, metaContainer.DataAccessLock);
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
        /// Clears the last updated timestamp cache for the specified file.
        /// </summary>
        /// <param name="file">File for which to clear last updated timestamp cache.</param>
        public void ClearLastUpdatedCache(PxFileRef file)
        {
            _cache.Remove(HashCode.Combine(LAST_UPDATED_SEED, file));
        }

        /// <summary>
        /// Clears the cached multilanguage database name for the specified database.
        /// </summary>
        /// <param name="dbRef">Database reference for which to clear the cached name.</param>
        public void ClearDatabaseNameCache(DataBaseRef dbRef)
        {
            _cache.Remove(HashCode.Combine(DATABASE_NAME_SEED, dbRef));
        }

        private void AttachEvictionOnFail(object cacheItem, Task task, object cacheKey, Lock? evictionLock = null)
        {
            task.ContinueWith(
                t =>
                {
                    // Observe the exception to prevent UnobservedTaskException
                    // The exceptions should be handled by the initial caller of the task, but just to be safe.
                    _ = t.Exception;

                    if (evictionLock is null)
                    {
                        RemoveIfCurrent();
                    }
                    else
                    {
                        lock (evictionLock)
                        {
                            RemoveIfCurrent();
                        }
                    }

                    void RemoveIfCurrent()
                    {
                        // Failed loads must not remove replacements that reuse the same key.
                        if (_cache.TryGetValue(cacheKey, out object? current) &&
                            ReferenceEquals(current, cacheItem))
                        {
                            _cache.Remove(cacheKey);
                        }
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously |
                TaskContinuationOptions.NotOnRanToCompletion,
                TaskScheduler.Default
            );
        }
        
        private void AttachEvictionOnFail(Task task, int cacheKey) =>
            AttachEvictionOnFail(task, task, cacheKey);


        private void OnMetaCacheEvicted(object? key, object? value, EvictionReason reason, object? state)
        {
            if (value is MetaCacheContainer metaContainer && state is PxFileRef file)
            {
                lock (metaContainer.DataAccessLock)
                {
                    foreach (IMatrixMap map in metaContainer.GetRelatedMaps())
                    {
                        _cache.Remove(GetDataCacheKey(file, metaContainer, map));
                    }
                }
            }
        }

        private static (string, PxFileRef, object, int) GetDataCacheKey(PxFileRef file, MetaCacheContainer metaContainer, IMatrixMap map) =>
            (DATA_SEED, file, metaContainer.Generation, MapHash(map));

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
