using Px.Utils.Language;
using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Metadata;
using Px.Utils.PxFile.Data;
using PxApi.Configuration;
using PxApi.DataSources;
using PxApi.Models;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text;

namespace PxApi.Caching
{
    /// <inheritdoc/>
    public class CachedDataSource(IDataBaseConnectorFactory dbConnectorFactory, DatabaseCache cache, ILogger<CachedDataSource> logger) : ICachedDataSource
    {
        private const string GROUPINGS_FILE = "groupings.json"; // Root level file listing groupings meta
        private const string GROUP_ALIAS_PREFIX = "Alias_"; // Files like Alias_fi.txt inside group folder
        private const string GROUP_ALIAS_SUFFIX = ".txt";
        private readonly Dictionary<string, DatabaseCacheConfig> cacheConfigs = 
            AppSettings.Active.DataBases.ToDictionary(
                db => db.Id,
                db => db.CacheConfig
            );

        private sealed record GroupingFileModel(string Code, Dictionary<string, string> Name);

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
                logger.LogDebug("File list cache hit.");
                return await files!;
            }

            logger.LogDebug("File list cache miss. Reading from database.");
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
                });

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
            return await container.Metadata;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<TableGroup>> GetGroupingsCachedAsync(PxFileRef pxFile, CancellationToken ct = default)
        {
            if (cache.TryGetGroupings(pxFile, out Task<IReadOnlyList<TableGroup>>? cachedTask))
            {
                logger.LogDebug("Groupings cache hit.");
                return await cachedTask!;
            }

            logger.LogDebug("Groupings cache miss. Reading from database.");
            IDataBaseConnector connector = dbConnectorFactory.GetConnector(pxFile.DataBase);
            Task<IReadOnlyList<TableGroup>> buildTask = BuildGroupingsAsync(pxFile, connector, ct);
            cache.SetGroupings(pxFile, buildTask);
            return await buildTask;
        }

        /// <inheritdoc/>
        public async Task<MultilanguageString> GetDatabaseNameAsync(DataBaseRef dataBase, string folderRelativePath, CancellationToken ct = default)
        {
            if (cache.TryGetDatabaseName(dataBase, out Task<MultilanguageString>? cachedName))
            {
                return await cachedName!;
            }

            IDataBaseConnector connector = dbConnectorFactory.GetConnector(dataBase);
            Task<MultilanguageString> buildTask = ReadAliasNameAsync(, connector, ct);
            cache.SetDatabaseName(dataBase, buildTask);
            return await buildTask;
        }

        /// <inheritdoc/>
        public async Task<DoubleDataValue[]> GetDataCachedAsync(PxFileRef pxFile, IMatrixMap map, CancellationToken ct = default)
        {
            if (cache.TryGetData(map, out Task<DoubleDataValue[]>? data, out DateTime? cached))
            {
                if (await CheckCacheValidity(pxFile, cached!.Value, ct))
                {
                    logger.LogDebug("Data cache exact hit.");
                    return await data!;
                }
                cache.TryRemoveMeta(pxFile);
            }
            else if (cache.TryGetDataSuperset(pxFile, map, out IMatrixMap? superMap, out Task<DoubleDataValue[]>? superData, out cached))
            {
                if (await CheckCacheValidity(pxFile, cached!.Value, ct))
                {
                    logger.LogDebug("Data cache superset hit.");
                    DataIndexer indexer = new(superMap!, map);
                    DoubleDataValue[] result = new DoubleDataValue[indexer.DataLength];
                    DoubleDataValue[] superDataArray = await superData!;
                    int index =0;
                    do result[index++] = superDataArray[indexer.CurrentIndex];
                    while (indexer.Next());
                    return result;
                }
                cache.TryRemoveMeta(pxFile);
            }

            logger.LogDebug("Data cache miss. Reading from database.");
            IDataBaseConnector dbConnector = dbConnectorFactory.GetConnector(pxFile.DataBase);
            MetaCacheContainer metaContainer = await GetMetaContainer(pxFile, ct);
            Task<DoubleDataValue[]> dataTask = dbConnector.ReadDataAsync(pxFile, map, await metaContainer.Metadata, ct);
            cache.SetData(pxFile, new MatrixMap([.. map.DimensionMaps]), dataTask);
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

        private async Task<MetaCacheContainer> GetMetaContainer(PxFileRef pxFile, CancellationToken ct = default)
        {
            if (cache.TryGetMetadata(pxFile, out MetaCacheContainer? metaContainer) &&
                await CheckCacheValidity(pxFile, metaContainer!.CachedUtc, ct))
            {
                logger.LogDebug("Metadata cache hit.");
                return metaContainer!;
            }

            logger.LogDebug("Metadata cache miss. Reading from database.");
            IDataBaseConnector dbConnector = dbConnectorFactory.GetConnector(pxFile.DataBase);
            Task<IReadOnlyMatrixMetadata> meta = dbConnector.ReadMetadataAsync(pxFile, ct);
            metaContainer = new MetaCacheContainer(meta);
            cache.SetMetadata(pxFile, metaContainer);
            return metaContainer;
        }

        private async Task<bool> CheckCacheValidity(PxFileRef file, DateTime cachedUtc, CancellationToken ct = default)
        {
            int? revalidationInterval = cacheConfigs[file.DataBase.Id].RevalidationIntervalMs;
            if (revalidationInterval is null || revalidationInterval == 0) return true;

            if (cache.TryGetLastUpdated(file, out Task<DateTime>? cachedTask))
            {
                return cachedUtc > await cachedTask!;
            }

            IDataBaseConnector dbConnector = dbConnectorFactory.GetConnector(file.DataBase);
            Task<DateTime> lastModified = dbConnector.GetLastWriteTimeAsync(file, ct);
            cache.SetLastUpdated(file, lastModified);
            return cachedUtc > await lastModified;
        }

        private static async Task<IReadOnlyList<TableGroup>> BuildGroupingsAsync(PxFileRef pxFile, IDataBaseConnector connector, CancellationToken ct = default)
        {
            if (pxFile.Hierarchy is null || pxFile.Hierarchy.Length == 0) return [];

            try
            {
                using Stream groupingStream = await connector.TryReadAuxiliaryFileAsync(GROUPINGS_FILE, [], ct);
                JsonSerializerOptions converterOptions = GlobalJsonConverterOptions.Default;
                GroupingFileModel? groupingModel = await JsonSerializer.DeserializeAsync<GroupingFileModel>(groupingStream, converterOptions, ct)
                    ?? throw new InvalidDataException($"Grouping file {GROUPINGS_FILE} is empty or malformed.");

                // Reuse alias reading logic
                MultilanguageString groupNameAliases = await ReadAliasNameAsync(pxFile.GetHierarchyLevels(), connector, ct);

                TableGroup group = new()
                {
                    Code = pxFile.Hierarchy,
                    Name = groupNameAliases,
                    GroupingCode = groupingModel.Code,
                    GroupingName = new(groupingModel.Name),
                    Links = []
                };

                List<TableGroup> groups = [group];
                return groups;
            }
            catch (FileNotFoundException)
            {
                return [];
            }
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