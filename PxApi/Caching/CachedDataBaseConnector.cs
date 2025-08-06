using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Metadata;
using Px.Utils.PxFile.Data;
using Px.Utils.PxFile.Metadata;
using PxApi.DataSources;
using PxApi.Models;
using System.Collections.Immutable;
using System.Text;

namespace PxApi.Caching
{
    /// <inheritdoc/>
    public class CachedDataBaseConnector(IDataBaseConnectorFactory dbConnectorFactory, DatabaseCache matrixCache) : ICachedDataBaseConnector
    {
        /// <inheritdoc/>
        public DataBaseRef? GetDataBaseReference(string dbId)
        {
            IReadOnlyCollection<DataBaseRef> databases = dbConnectorFactory.GetAvailableDatabases();
            if(databases.Any(db => db.Id.Equals(dbId, StringComparison.OrdinalIgnoreCase)))
            {
                return databases.First(db => db.Id.Equals(dbId, StringComparison.OrdinalIgnoreCase));
            }
            return null;
        }

        /// <inheritdoc/>
        public async Task<ImmutableSortedDictionary<string, PxFileRef>> GetFileListCachedAsync(DataBaseRef dataBase)
        {
            if (matrixCache.TryGetFileList(out Task<ImmutableSortedDictionary<string, PxFileRef>>? files))
            {
                return await files!;
            }

            IDataBaseConnector dbConnector = dbConnectorFactory.GetConnector(dataBase);
            Task<ImmutableSortedDictionary<string, PxFileRef>> fileListTask = 
                dbConnector.GetAllFilesAsync()
                .ContinueWith(t => t.Result.ToImmutableSortedDictionary(
                    file => Path.GetFileNameWithoutExtension(file),
                    file => PxFileRef.Create(Path.GetFileNameWithoutExtension(file), dbConnector.DataBase)));
            matrixCache.SetFileList(dataBase, fileListTask);
            return await fileListTask;
        }

        /// <inheritdoc/>
        public async Task<PxFileRef?> GetFileReferenceCachedAsync(string fileId, DataBaseRef db)
        {
            ImmutableSortedDictionary<string, PxFileRef> files = await GetFileListCachedAsync(db);
            if (files.TryGetValue(fileId, out PxFileRef file)) return file;
            else throw new KeyNotFoundException("File with not found in database.");
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyMatrixMetadata> GetMetadataCachedAsync(PxFileRef pxFile)
        {
            MetaCacheContainer container = await GetMetaContainer(pxFile);
            return await container.Metadata;
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
            metaContainer.DataSectionOffset ??= await reader.GetDataSectionOffset(pxFile);

            Task<DoubleDataValue[]> dataTask = reader.ReadDataAsync(pxFile, metaContainer.DataSectionOffset.Value, map, await metaContainer.Metadata);
            matrixCache.SetData(pxFile, new([.. map.DimensionMaps]), dataTask);
            return await dataTask;
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
            Task<IReadOnlyMatrixMetadata> meta = reader.ReadMetadata(pxFile);
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
    }
} 