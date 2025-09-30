using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Metadata;
using Px.Utils.PxFile.Data;
using Px.Utils.PxFile.Metadata;
using PxApi.Configuration;
using PxApi.DataSources;
using PxApi.Models;
using System.Collections.Immutable;
using System.Text;
using Px.Utils.Language;
using System.Text.Json;

namespace PxApi.Caching
{
    /// <inheritdoc/>
    public class CachedDataSource(IDataBaseConnectorFactory dbConnectorFactory, DatabaseCache matrixCache) : ICachedDataSource
    {
        private const string GROUPINGS_FILE = "groupings.json"; // Root level file listing groupings meta
        private const string GROUP_ALIAS_PREFIX = "Alias_"; // Files like Alias_fi.txt inside group folder
        private const string GROUP_ALIAS_SUFFIX = ".txt";

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
            if (matrixCache.TryGetFileList(dataBase, out Task<ImmutableSortedDictionary<string, PxFileRef>>? files))
            {
                return await files!;
            }

            IDataBaseConnector dbConnector = dbConnectorFactory.GetConnector(dataBase);
            Task<ImmutableSortedDictionary<string, PxFileRef>> fileListTask = dbConnector.GetAllFilesAsync().ContinueWith(t =>
            {
                Dictionary<string, PxFileRef> fileDict = [];
                foreach (string file in t.Result)
                {
                    PxFileRef fileRef = PxFileRef.CreateFromId(Path.GetFileNameWithoutExtension(file), dbConnector.DataBase);
                    fileDict.TryAdd(fileRef.Id, fileRef);
                }
                return fileDict.ToImmutableSortedDictionary();
            });

            matrixCache.SetFileList(dataBase, fileListTask);
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
            if (matrixCache.TryGetGroupings(pxFile, out Task<IReadOnlyList<TableGroup>>? cachedTask))
            {
                return await cachedTask!;
            }

            IDataBaseConnector connector = dbConnectorFactory.GetConnector(pxFile.DataBase);
            Task<IReadOnlyList<TableGroup>> buildTask = BuildGroupingsAsync(pxFile, connector);
            matrixCache.SetGroupings(pxFile, buildTask);
            return await buildTask;
        }

        /// <inheritdoc/>
        public async Task<string> GetSingleStringValueAsync(string key, PxFileRef file)
        {
            IDataBaseConnector dbConnector = dbConnectorFactory.GetConnector(file.DataBase);
            using Stream fileStream = dbConnector.ReadPxFile(file);
            PxFileMetadataReader reader = new();
            Encoding encoding = await reader.GetEncodingAsync(fileStream);

            if (fileStream.CanSeek) fileStream.Seek(0, SeekOrigin.Begin);
            else throw new InvalidOperationException("Not able to seek in the filestream");

            IAsyncEnumerable<KeyValuePair<string, string>> metaEntries = reader.ReadMetadataAsync(fileStream, encoding);
            return (await metaEntries.FirstAsync(pair => pair.Key == key)).Value;
        }

        /// <inheritdoc/>
        public async Task<DoubleDataValue[]> GetDataCachedAsync(PxFileRef pxFile, IMatrixMap map)
        {
            if (matrixCache.TryGetData(map, out Task<DoubleDataValue[]>? data, out DateTime? cached))
            {
                if (cached! > await GetLastModified(pxFile))
                {
                    return await data!;
                }
                matrixCache.TryRemoveMeta(pxFile);
            }
            else if (matrixCache.TryGetDataSuperset(pxFile, map, out IMatrixMap? superMap, out Task<DoubleDataValue[]>? superData, out cached))
            {
                if (cached! > await GetLastModified(pxFile))
                {
                    DataIndexer indexer = new(superMap!, map);
                    DoubleDataValue[] result = new DoubleDataValue[indexer.DataLength];
                    DoubleDataValue[] superDataArray = await superData!;
                    int index = 0;
                    do result[index++] = superDataArray[indexer.CurrentIndex];
                    while (indexer.Next());
                    return result;
                }
                matrixCache.TryRemoveMeta(pxFile);
            }

            IDataBaseConnector dbConnector = dbConnectorFactory.GetConnector(pxFile.DataBase);
            MetaCacheContainer metaContainer = await GetMetaContainer(pxFile);
            PxFileReader reader = new(dbConnector);
            metaContainer.DataSectionOffset ??= await reader.GetDataSectionOffsetAsync(pxFile);

            Task<DoubleDataValue[]> dataTask = reader.ReadDataAsync(pxFile, metaContainer.DataSectionOffset.Value, map, await metaContainer.Metadata);
            matrixCache.SetData(pxFile, new([.. map.DimensionMaps]), dataTask);
            return await dataTask;
        }

        /// <inheritdoc/>
        public void ClearFileListCache(DataBaseRef dbRef)
        {
            matrixCache.ClearFileListCache(dbRef);
        }

        /// <inheritdoc/>
        public async Task ClearMetadataCacheAsync(DataBaseRef dataBase)
        {
            ImmutableSortedDictionary<string, PxFileRef> files = await GetFileListCachedAsync(dataBase);
            foreach (PxFileRef file in files.Values)
            {
                matrixCache.TryRemoveMeta(file);
            }
        }

        /// <inheritdoc/>
        public async Task ClearDataCacheAsync(DataBaseRef dataBase)
        {
            ImmutableSortedDictionary<string, PxFileRef> files = await GetFileListCachedAsync(dataBase);
            foreach (PxFileRef file in files.Values)
            {
                if (matrixCache.TryGetMetadata(file, out MetaCacheContainer? metaContainer) && metaContainer is not null)
                {
                    matrixCache.TryRemoveMeta(file);
                    Task<IReadOnlyMatrixMetadata> meta = metaContainer.Metadata;
                    MetaCacheContainer newContainer = new(meta);
                    matrixCache.SetMetadata(file, newContainer);
                }
            }
        }

        /// <inheritdoc/>
        public async Task ClearAllCache(DataBaseRef dataBase)
        {
            await ClearDataCacheAsync(dataBase);
            await ClearMetadataCacheAsync(dataBase);
            ClearFileListCache(dataBase);
        }

        /// <inheritdoc/>
        public async Task ClearAllCachesAsync()
        {
            IReadOnlyCollection<DataBaseRef> allDatabases = GetAllDataBaseReferences();
            foreach (DataBaseRef dbRef in allDatabases)
            {
                await ClearAllCache(dbRef);
            }
        }

        /// <inheritdoc/>
        public async Task ClearLastUpdatedCacheAsync(DataBaseRef dataBase)
        {
            ImmutableSortedDictionary<string, PxFileRef> files = await GetFileListCachedAsync(dataBase);
            matrixCache.ClearLastUpdatedCache(files.Values);
        }

        private async Task<MetaCacheContainer> GetMetaContainer(PxFileRef pxFile)
        {
            if (matrixCache.TryGetMetadata(pxFile, out MetaCacheContainer? metaContainer) &&
                (metaContainer!.CachedUtc > await GetLastModified(pxFile)))
            {
                return metaContainer!;
            }

            IDataBaseConnector dbConnector = dbConnectorFactory.GetConnector(pxFile.DataBase);
            PxFileReader reader = new(dbConnector);
            Task<IReadOnlyMatrixMetadata> meta = reader.ReadMetadataAsync(pxFile);
            metaContainer = new(meta);
            matrixCache.SetMetadata(pxFile, metaContainer);
            return metaContainer;
        }

        private async Task<DateTime> GetLastModified(PxFileRef file)
        {
            if (matrixCache.TryGetLastUpdated(file, out Task<DateTime>? cachedTask))
            {
                return await cachedTask!;
            }

            IDataBaseConnector dbConnector = dbConnectorFactory.GetConnector(file.DataBase);
            Task<DateTime> lastModified = dbConnector.GetLastWriteTimeAsync(file);
            matrixCache.SetLastUpdated(file, lastModified);
            return await lastModified;
        }

        private static async Task<IReadOnlyList<TableGroup>> BuildGroupingsAsync(PxFileRef pxFile, IDataBaseConnector connector)
        {
            try
            {
                using Stream groupingStream = await connector.TryReadAuxiliaryFileAsync(GROUPINGS_FILE);
                GroupingFileModel? groupingModel = await JsonSerializer.DeserializeAsync<GroupingFileModel>(groupingStream);
                if (groupingModel is null) return [];

                string? fileDirPath = string.IsNullOrEmpty(pxFile.FilePath) ? null : Path.GetDirectoryName(pxFile.FilePath);
                string? groupFolderName = fileDirPath is null ? null : new DirectoryInfo(fileDirPath).Name;
                if (groupFolderName is null) return [];

                Dictionary<string, string> aliasTranslations = new(StringComparer.OrdinalIgnoreCase);
                foreach (string lang in groupingModel.name.Keys)
                {
                    string aliasFileRelPath = groupFolderName + "/" + GROUP_ALIAS_PREFIX + lang + GROUP_ALIAS_SUFFIX;
                    using Stream aliasStream = await connector.TryReadAuxiliaryFileAsync(aliasFileRelPath);
                    using StreamReader sr = new(aliasStream, Encoding.UTF8, true);
                    string? alias = await sr.ReadLineAsync();
                    if (!string.IsNullOrWhiteSpace(alias))
                    {
                        aliasTranslations[lang] = alias.Trim();
                    }
                }

                MultilanguageString groupingName = new(groupingModel.name);
                TableGroup group = new()
                {
                    Code = groupFolderName,
                    Name = new(aliasTranslations),
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
    }
}