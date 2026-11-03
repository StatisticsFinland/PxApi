using Px.Utils.Language;
using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Metadata;
using Px.Utils.PxFile.Data;
using PxApi.Configuration;
using PxApi.DataSources;
using PxApi.Models;
using System.Collections.Immutable;
using System.Text;

namespace PxApi.Caching
{
    /// <inheritdoc/>
    public class CachedDataSource(IDataBaseConnectorFactory dbConnectorFactory, DatabaseCache cache, ICacheObserver? cacheObserver = null) : ICachedDataSource
    {
        private const string GROUP_ALIAS_PREFIX = "Alias_"; // Files like Alias_fi.txt inside group folder
        private const string GROUP_ALIAS_SUFFIX = ".txt";
        private readonly Dictionary<string, DatabaseCacheConfig> cacheConfigs = 
            AppSettings.Active.DataBases.ToDictionary(
                db => db.Id,
                db => db.CacheConfig
            );

        /// <inheritdoc/>
        public DataBaseRef? GetDataBaseReference(string dbId)
        {
            IReadOnlyCollection<DataBaseRef> databases = dbConnectorFactory.GetAvailableDatabases();
            if (databases.Any(db => db.Id.Equals(dbId, StringComparison.OrdinalIgnoreCase)))
            {
                return databases.First(db => db.Id.Equals(dbId, StringComparison.OrdinalIgnoreCase));
            }
            return null;
        }

        /// <inheritdoc/>
        public IReadOnlyCollection<DataBaseRef> GetAllDataBaseReferences()
        {
            return dbConnectorFactory.GetAvailableDatabases();
        }

        /// <inheritdoc/>
        public async Task<ImmutableSortedDictionary<string, PxFileRef>> GetFileListCachedAsync(DataBaseRef dataBase, CancellationToken ct = default)
        {
            if (cache.TryGetFileList(dataBase, out Task<ImmutableSortedDictionary<string, PxFileRef>>? files))
            {
                cacheObserver?.RecordFileListLookup(true);
                return await files!;
            }

            cacheObserver?.RecordFileListLookup(false);
            IDataBaseConnector dbConnector = dbConnectorFactory.GetConnector(dataBase);
            Task<ImmutableSortedDictionary<string, PxFileRef>> fileListTask = dbConnector.GetAllFilesAsync(ct)
                .ContinueWith(t =>
                {
                    Dictionary<string, PxFileRef> fileDict = [];
                    foreach (PxFileRef file in t.Result)
                    {
                        fileDict.TryAdd(file.Id, file);
                    }
                    return fileDict.ToImmutableSortedDictionary();
                }, CancellationToken.None);

            cache.SetFileList(dataBase, fileListTask);
            return await fileListTask;
        }

        /// <inheritdoc/>
        public async Task<PxFileRef?> GetFileReferenceCachedAsync(string fileId, DataBaseRef db, CancellationToken ct = default)
        {
            ImmutableSortedDictionary<string, PxFileRef> files = await GetFileListCachedAsync(db, ct);
            if (files.TryGetValue(fileId, out PxFileRef file)) return file;
            return null;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyMatrixMetadata> GetMetadataCachedAsync(PxFileRef pxFile, CancellationToken ct = default)
        {
            MetaCacheContainer container = await GetMetaContainer(pxFile, ct);
            return await container.Metadata.WaitAsync(ct);
        }

        /// <inheritdoc/>
        public async Task<MultilanguageString> GetDatabaseNameAsync(DataBaseRef dataBase, string folderRelativePath, CancellationToken ct = default)
        {
            if (cache.TryGetDatabaseName(dataBase, out Task<MultilanguageString>? cachedName))
            {
                return await cachedName!;
            }

            IDataBaseConnector connector = dbConnectorFactory.GetConnector(dataBase);
            Task<MultilanguageString> buildTask = ReadAliasNameAsync([], connector, ct);
            cache.SetDatabaseName(dataBase, buildTask);
            return await buildTask;
        }

        /// <inheritdoc/>
        public async Task<DoubleDataValue[]> GetDataCachedAsync(PxFileRef pxFile, IMatrixMap map, CancellationToken ct = default)
        {
            // Data entries are scoped to a metadata generation, so revalidating (and if needed replacing) the
            // metadata first makes any data found below at least as fresh as the validated metadata.
            MetaCacheContainer metaContainer = await GetMetaContainer(pxFile, ct, recordHit: false); // Hits are recorded by the metadata request

            if (cache.TryGetData(pxFile, map, out Task<DoubleDataValue[]>? data, out _))
            {
                cacheObserver?.RecordDataCacheHit(isSuperset: false);
                return await data!;
            }
            else if (cache.TryGetDataSuperset(pxFile, map, out IMatrixMap? superMap, out Task<DoubleDataValue[]>? superData, out _))
            {
                cacheObserver?.RecordDataCacheHit(isSuperset: true);
                DataIndexer indexer = new(superMap!, map);
                DoubleDataValue[] result = new DoubleDataValue[indexer.DataLength];
                DoubleDataValue[] superDataArray = await superData!;
                int index =0;
                do result[index++] = superDataArray[indexer.CurrentIndex];
                while (indexer.Next());
                return result;
            }

            cacheObserver?.RecordDataCacheMiss();
            IDataBaseConnector dbConnector = dbConnectorFactory.GetConnector(pxFile.DataBase);
            Task<DoubleDataValue[]> dataTask = dbConnector.ReadDataAsync(pxFile, map, await metaContainer.Metadata.WaitAsync(ct), ct);
            cache.SetData(pxFile, metaContainer, new MatrixMap([.. map.DimensionMaps]), dataTask);
            return await dataTask;
        }

        /// <inheritdoc/>
        public async Task ClearDatabaseCacheAsync(DataBaseRef dataBase)
        {
            // Get all files for the database and clear their metadata. This removes data cache as well
            ImmutableSortedDictionary<string, PxFileRef> files = await GetFileListCachedAsync(dataBase);
            foreach (PxFileRef file in files.Values)
            {
                ClearMetadataCacheAsync(file);
                ClearLastUpdatedCacheAsync(file);
            }
            cache.ClearDatabaseNameCache(dataBase);
            cache.ClearFileListCache(dataBase);
        }

        /// <inheritdoc/>
        public void ClearTableListCache(DataBaseRef dataBase)
        {
            cache.ClearFileListCache(dataBase);
        }

        /// <inheritdoc />
        public void ClearTableCache(PxFileRef file)
        {
            ClearMetadataCacheAsync(file);
            ClearLastUpdatedCacheAsync(file);
        }

        private void ClearMetadataCacheAsync(PxFileRef file)
        {
            cache.TryRemoveMeta(file);
        }

        private void ClearLastUpdatedCacheAsync(PxFileRef file)
        {
            cache.ClearLastUpdatedCache(file);
        }

        private async Task<MetaCacheContainer> GetMetaContainer(PxFileRef pxFile, CancellationToken ct = default, bool recordHit = true)
        {
            if (cache.TryGetMetadata(pxFile, out MetaCacheContainer? metaContainer) &&
                await IsSourceUnchanged(pxFile, metaContainer!, ct))
            {
                if (recordHit) cacheObserver?.RecordMetadataLookup(true);
                return metaContainer!;
            }

            cacheObserver?.RecordMetadataLookup(false);
            // Observe the version before reading so a concurrent source update can only cause an extra reload, never a missed one.
            DateTime? sourceVersion = IsRevalidationEnabled(pxFile) ? await GetSourceVersionAsync(pxFile, ct) : null;

            IDataBaseConnector dbConnector = dbConnectorFactory.GetConnector(pxFile.DataBase);
            // Task.Run keeps connector work, including synchronous prefixes or throws, outside the publication lock.
            // Shared reads outlive individual requests; cancellation only stops each caller's wait.
            return cache.GetOrSetMetadata(pxFile, sourceVersion,
                () => new MetaCacheContainer(Task.Run(() => dbConnector.ReadMetadataAsync(pxFile, CancellationToken.None), CancellationToken.None), sourceVersion));
        }

        private bool IsRevalidationEnabled(PxFileRef file) =>
            cacheConfigs[file.DataBase.Id].RevalidationIntervalMs is int interval && interval != 0;

        // Compares source versions for equality instead of against the clock, because some sources
        // (e.g. LAST-UPDATED in binary blob databases) report local or future-dated timestamps.
        private async Task<bool> IsSourceUnchanged(PxFileRef file, MetaCacheContainer metaContainer, CancellationToken ct)
        {
            if (!IsRevalidationEnabled(file) || metaContainer.SourceVersion is null) return true;
            return await GetSourceVersionAsync(file, ct) == metaContainer.SourceVersion;
        }

        private Task<DateTime> GetSourceVersionAsync(PxFileRef file, CancellationToken ct)
        {
            if (cache.TryGetLastUpdated(file, out Task<DateTime>? cachedTask))
            {
                return cachedTask!.WaitAsync(ct);
            }

            IDataBaseConnector dbConnector = dbConnectorFactory.GetConnector(file.DataBase);
            Task<DateTime> lastModified = dbConnector.GetLastWriteTimeAsync(file, CancellationToken.None);
            cache.SetLastUpdated(file, lastModified);
            return lastModified.WaitAsync(ct);
        }

        // Reads alias files (Alias_{lang}.txt) from a folder and builds a MultilanguageString from the first line of each file.
        private static async Task<MultilanguageString> ReadAliasNameAsync(string[]? hierarchy, IDataBaseConnector connector, CancellationToken ct = default)
        {
            Dictionary<string, string> translations = new(StringComparer.OrdinalIgnoreCase);
            foreach (string lang in new string[] { "fi", "sv", "en" })
            {
                string fileName = GROUP_ALIAS_PREFIX + lang + GROUP_ALIAS_SUFFIX;
                using Stream aliasStream = await connector.TryReadAuxiliaryFileAsync(fileName, hierarchy, ct);
                using StreamReader sr = new(aliasStream, Encoding.UTF8, true);
                string? alias = await sr.ReadLineAsync(ct);
                if (!string.IsNullOrWhiteSpace(alias))
                {
                    translations[lang] = alias.Trim();
                }
            }
            return new MultilanguageString(translations);
        }
    }
}