using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Metadata;
using Px.Utils.PxFile.Data;
using Px.Utils.PxFile.Metadata;
using PxApi.DataSources;
using PxApi.Models;
using System.Collections.Immutable;
using System.Text;
using Px.Utils.Language;
using System.Text.Json;
using PxApi.Configuration;

namespace PxApi.Caching
{
    /// <inheritdoc/>
    public class CachedDataSource(IDataBaseConnectorFactory dbConnectorFactory, DatabaseCache cache) : ICachedDataSource
    {
        private const string GROUPINGS_FILE = "groupings.json"; // Root level file listing groupings meta
        private const string GROUP_ALIAS_PREFIX = "Alias_"; // Files like Alias_fi.txt inside group folder
        private const string GROUP_ALIAS_SUFFIX = ".txt";
        private readonly Dictionary<string, DatabaseCacheConfig> cacheConfigs = 
            AppSettings.Active.DataBases.ToDictionary(
                db => db.Id,
                db => db.CacheConfig
            );

        private sealed record GroupingFileModel(string code, Dictionary<string, string> name);

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
        public async Task<ImmutableSortedDictionary<string, PxFileRef>> GetFileListCachedAsync(DataBaseRef dataBase)
        {
            if (cache.TryGetFileList(dataBase, out Task<ImmutableSortedDictionary<string, PxFileRef>>? files))
            {
                return await files!;
            }

            IDataBaseConnector dbConnector = dbConnectorFactory.GetConnector(dataBase);
            Task<ImmutableSortedDictionary<string, PxFileRef>> fileListTask = dbConnector.GetAllFilesAsync().ContinueWith(t =>
            {
                Dictionary<string, PxFileRef> fileDict = [];
                foreach (string file in t.Result)
                {
                    PxFileRef fileRef = PxFileRef.CreateFromPath(file, dbConnector.DataBase);
                    fileDict.TryAdd(fileRef.Id, fileRef);
                }
                return fileDict.ToImmutableSortedDictionary();
            });

            cache.SetFileList(dataBase, fileListTask);
            return await fileListTask;
        }

        /// <inheritdoc/>
        public async Task<PxFileRef?> GetFileReferenceCachedAsync(string fileId, DataBaseRef db)
        {
            ImmutableSortedDictionary<string, PxFileRef> files = await GetFileListCachedAsync(db);
            if (files.TryGetValue(fileId, out PxFileRef file)) return file;
            return null;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyMatrixMetadata> GetMetadataCachedAsync(PxFileRef pxFile)
        {
            MetaCacheContainer container = await GetMetaContainer(pxFile);
            return await container.Metadata;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<TableGroup>> GetGroupingsCachedAsync(PxFileRef pxFile)
        {
            if (cache.TryGetGroupings(pxFile, out Task<IReadOnlyList<TableGroup>>? cachedTask))
            {
                return await cachedTask!;
            }

            IDataBaseConnector connector = dbConnectorFactory.GetConnector(pxFile.DataBase);
            Task<IReadOnlyList<TableGroup>> buildTask = BuildGroupingsAsync(pxFile, connector);
            cache.SetGroupings(pxFile, buildTask);
            return await buildTask;
        }

        /// <inheritdoc/>
        public async Task<MultilanguageString> GetDatabaseNameAsync(DataBaseRef dataBase, string folderRelativePath)
        {
            if (cache.TryGetDatabaseName(dataBase, out Task<MultilanguageString>? cachedName))
            {
                return await cachedName!;
            }

            IDataBaseConnector connector = dbConnectorFactory.GetConnector(dataBase);
            Task<MultilanguageString> buildTask = ReadAliasNameAsync(folderRelativePath, connector);
            cache.SetDatabaseName(dataBase, buildTask);
            return await buildTask;
        }

        /// <inheritdoc/>
        public async Task<string> GetSingleStringValueAsync(string key, PxFileRef file)
        {
            IDataBaseConnector dbConnector = dbConnectorFactory.GetConnector(file.DataBase);
            using Stream fileStream = dbConnector.ReadPxFile(file);
            PxFileMetadataReader reader = new PxFileMetadataReader();
            Encoding encoding = await reader.GetEncodingAsync(fileStream);

            if (fileStream.CanSeek) fileStream.Seek(0, SeekOrigin.Begin);
            else throw new InvalidOperationException("Not able to seek in the filestream");

            IAsyncEnumerable<KeyValuePair<string, string>> metaEntries = reader.ReadMetadataAsync(fileStream, encoding);
            return (await metaEntries.FirstAsync(pair => pair.Key == key)).Value;
        }

        /// <inheritdoc/>
        public async Task<DoubleDataValue[]> GetDataCachedAsync(PxFileRef pxFile, IMatrixMap map)
        {
            if (cache.TryGetData(map, out Task<DoubleDataValue[]>? data, out DateTime? cached))
            {
                if (await CheckCacheValidity(pxFile, cached!.Value))
                {
                    return await data!;
                }
                cache.TryRemoveMeta(pxFile);
            }
            else if (cache.TryGetDataSuperset(pxFile, map, out IMatrixMap? superMap, out Task<DoubleDataValue[]>? superData, out cached))
            {
                if (await CheckCacheValidity(pxFile, cached!.Value))
                {
                    DataIndexer indexer = new DataIndexer(superMap!, map);
                    DoubleDataValue[] result = new DoubleDataValue[indexer.DataLength];
                    DoubleDataValue[] superDataArray = await superData!;
                    int index =0;
                    do result[index++] = superDataArray[indexer.CurrentIndex];
                    while (indexer.Next());
                    return result;
                }
                cache.TryRemoveMeta(pxFile);
            }

            IDataBaseConnector dbConnector = dbConnectorFactory.GetConnector(pxFile.DataBase);
            MetaCacheContainer metaContainer = await GetMetaContainer(pxFile);
            PxFileReader reader = new PxFileReader(dbConnector);
            metaContainer.DataSectionOffset ??= await reader.GetDataSectionOffsetAsync(pxFile);

            Task<DoubleDataValue[]> dataTask = reader.ReadDataAsync(pxFile, metaContainer.DataSectionOffset.Value, map, await metaContainer.Metadata);
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

        private async Task<MetaCacheContainer> GetMetaContainer(PxFileRef pxFile)
        {
            if (cache.TryGetMetadata(pxFile, out MetaCacheContainer? metaContainer) &&
                await CheckCacheValidity(pxFile, metaContainer!.CachedUtc))
            {
                return metaContainer!;
            }

            IDataBaseConnector dbConnector = dbConnectorFactory.GetConnector(pxFile.DataBase);
            PxFileReader reader = new PxFileReader(dbConnector);
            Task<IReadOnlyMatrixMetadata> meta = reader.ReadMetadataAsync(pxFile);
            metaContainer = new MetaCacheContainer(meta);
            cache.SetMetadata(pxFile, metaContainer);
            return metaContainer;
        }

        private async Task<bool> CheckCacheValidity(PxFileRef file, DateTime cachedUtc)
        {
            int? revalidationInterval = cacheConfigs[file.DataBase.Id].RevalidationIntervalMs;
            if (revalidationInterval is null || revalidationInterval ==0) return true;

            if (cache.TryGetLastUpdated(file, out Task<DateTime>? cachedTask))
            {
                return cachedUtc > await cachedTask!;
            }

            IDataBaseConnector dbConnector = dbConnectorFactory.GetConnector(file.DataBase);
            Task<DateTime> lastModified = dbConnector.GetLastWriteTimeAsync(file);
            cache.SetLastUpdated(file, lastModified);
            return cachedUtc > await lastModified;
        }

        private static async Task<IReadOnlyList<TableGroup>> BuildGroupingsAsync(PxFileRef pxFile, IDataBaseConnector connector)
        {
            try
            {
                using Stream groupingStream = await connector.TryReadAuxiliaryFileAsync(GROUPINGS_FILE);
                GroupingFileModel? groupingModel = await JsonSerializer.DeserializeAsync<GroupingFileModel>(groupingStream);
                string? fileDirName = Path.GetDirectoryName(pxFile.FilePath)?
                    .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)[^1];
                if (groupingModel is null || fileDirName is null) return [];

                // Reuse alias reading logic
                MultilanguageString groupNameAliases = await ReadAliasNameAsync(fileDirName,connector);

                MultilanguageString groupingName = new(groupingModel.name);
                TableGroup group = new()
                {
                    Code = fileDirName,
                    Name = groupNameAliases,
                    GroupingCode = groupingModel.code,
                    GroupingName = groupingName,
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
        private static async Task<MultilanguageString> ReadAliasNameAsync(string folderRelativePath, IDataBaseConnector connector)
        {
            Dictionary<string, string> translations = new(StringComparer.OrdinalIgnoreCase);
            foreach (string lang in new string[] { "fi", "sv", "en" })
            {
                string aliasFilePath = Path.Combine(folderRelativePath, GROUP_ALIAS_PREFIX + lang + GROUP_ALIAS_SUFFIX);
                using Stream aliasStream = await connector.TryReadAuxiliaryFileAsync(aliasFilePath);
                using StreamReader sr = new(aliasStream, Encoding.UTF8, true);
                string? alias = await sr.ReadLineAsync();
                if (!string.IsNullOrWhiteSpace(alias))
                {
                    translations[lang] = alias.Trim();
                }
            }
            return new MultilanguageString(translations);
        }
    }
}