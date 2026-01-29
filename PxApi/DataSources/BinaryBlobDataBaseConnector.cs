using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using Azure;
using Microsoft.Extensions.Azure;
using Px.Utils.BinaryData.ValueConverters;
using Px.Utils.BinaryData;
using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Metadata.Dimensions;
using Px.Utils.Models.Metadata.ExtensionMethods;
using Px.Utils.Models.Metadata;
using PxApi.Exceptions;
using PxApi.Models;
using PxApi.Utilities;
using System.Text.Json;

namespace PxApi.DataSources
{
    public class BinaryBlobDataBaseConnector(DataBaseRef dataBase, string containerName, IAzureClientFactory<BlobServiceClient> blobServiceClientFactory, ILogger<PxBlobDataBaseConnector> logger)
        : BlobDataBaseConnector(dataBase, containerName, blobServiceClientFactory)
    {
        protected override ILogger Logger => logger;

        private const string MetaPrefix = "meta/";
        private const string MetaFileSuffix = ".meta.json";

        private const string DataPrefix = "bin/";
        private const string DataFileSuffix = ".pxb";

        private const string PxPrefix = "px/";

        /// <inheritdoc/>
        public override async Task<string[]> GetAllFilesAsync(CancellationToken ct)
        {
            using (Logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.CONTROLLER] = nameof(PxBlobDataBaseConnector),
                    [LoggerConsts.FUNCTION] = nameof(GetAllFilesAsync),
                    [LoggerConsts.CONTAINER_NAME] = ContainerName
                }))
            {
                Logger.LogDebug("Getting all meta files from blob storage container.");
                List<string> fileNames = [];

                BlobContainerClient containerClient = GetContainerClient();
                AsyncPageable<BlobItem> blobs = containerClient.GetBlobsAsync(prefix: MetaPrefix, cancellationToken: ct);

                await foreach (BlobItem blob in blobs)
                {
                    if (blob.Name.EndsWith(MetaFileSuffix, StringComparison.OrdinalIgnoreCase))
                    {
                        fileNames.Add(blob.Name);
                    }
                }

                Logger.LogDebug("Found {Count} meta files.", fileNames.Count);
                return [.. fileNames];
            }
        }

        /// <inheritdoc/>
        public override async Task<DateTime> GetLastWriteTimeAsync(PxFileRef file, CancellationToken ct)
        {
            using (Logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.CONTROLLER] = nameof(PxBlobDataBaseConnector),
                    [LoggerConsts.FUNCTION] = nameof(GetLastWriteTimeAsync),
                    [LoggerConsts.PX_FILE] = file.Id,
                    [LoggerConsts.CONTAINER_NAME] = ContainerName
                }))
            {
                Logger.LogDebug("Getting last write time for meta file {FileId} from blob storage", file.Id);

                BlobContainerClient containerClient = GetContainerClient();
                string blobName = MetaPrefix + file.Id + MetaFileSuffix;
                BlobClient blobClient = containerClient.GetBlobClient(blobName);

                if (!await blobClient.ExistsAsync(ct))
                {
                    Logger.LogError("Meta file for id {FileId} not found in blob storage", file.Id);
                    throw new FileNotFoundException($"Meta file for id {file.Id} not found in blob storage container.");
                }

                BlobProperties properties = await blobClient.GetPropertiesAsync(cancellationToken: ct);
                return properties.LastModified.DateTime;
            }
        }

        /// <inheritdoc/>
        public async override Task<DoubleDataValue[]> ReadDataAsync(PxFileRef file, IMatrixMap targetMap, IReadOnlyMatrixMetadata meta, CancellationToken ct)
        {
            IReadOnlyDimension contentDimension = meta.GetContentDimension();
            DoubleDataValue[] result = new DoubleDataValue[targetMap.GetSize()];

            foreach (string cValCode in contentDimension.ValueCodes)
            {
                ct.ThrowIfCancellationRequested();

                string blobName = DataPrefix + cValCode;
                DateTime lastUpdated = DateTime.Now;
                BlobContainerClient containerClient = GetContainerClient();
                BlobClient blob = containerClient.GetBlobClient(blobName);
                if (!await blob.ExistsAsync(ct))
                {
                    Logger.LogError("Data blob {BlobName} not found in blob storage.", blobName);
                    throw new BinaryBlobSynchronizationException(file, lastUpdated);
                }

                IMatrixMap readMap = targetMap.CollapseDimension(contentDimension.Code, cValCode);
                IMatrixMap blobMap = meta.CollapseDimension(contentDimension.Code, cValCode);

                if (BlobReadModeSelector.ReadStreaming(readMap, blobMap, out long startIndex))
                { 
                    using Stream blobStream = await blob.OpenReadAsync(cancellationToken: ct);
                    byte[] headerBytes = new byte[8];
                    await blobStream.ReadExactlyAsync(headerBytes, ct);
                    uint headerLength = BitConverter.ToUInt32(headerBytes, 0);
                    BinaryValueCodecType codec = (BinaryValueCodecType)BitConverter.ToUInt32(headerBytes, 4);
                    BinaryDataReader reader = BinaryDataReader.Create(codec);

                    await reader.ReadFromStreamAsync(blobStream, readMap, blobMap, targetMap, result, ct);
                }
                else
                {
                    async Task<Stream> readerFunc(long offset, long length, CancellationToken ct)
                    {
                        Response<BlobDownloadStreamingResult> result = await blob.DownloadStreamingAsync(new HttpRange(offset, length), null, false, ct);
                        return result.Value.Content;
                    }

                    byte[] headerBytes = new byte[8];
                    using Stream headerStream = await readerFunc(0, 8, ct);
                    await headerStream.ReadExactlyAsync(headerBytes, ct);

                    uint headerLength = BitConverter.ToUInt32(headerBytes, 0);
                    BinaryValueCodecType codec = (BinaryValueCodecType)BitConverter.ToUInt32(headerBytes, 4);

                    BinaryDataReader reader = BinaryDataReader.Create(codec, headerLengthBytes: headerLength);
                    await reader.ReadByChunkAsync(readerFunc, readMap, blobMap, targetMap, result, ct);
                }
            }
            throw new NotImplementedException("BinaryBlobDataBaseConnector does not support reading data.");
        }

        /// <inheritdoc/>
        public override async Task<IReadOnlyMatrixMetadata> ReadMetadataAsync(PxFileRef file, CancellationToken ct)
        {
            using (Logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.FUNCTION] = nameof(ReadMetadataAsync),
                    [LoggerConsts.PX_FILE] = file.Id,
                    [LoggerConsts.CONTAINER_NAME] = ContainerName
                }))
            {
                Logger.LogDebug("Reading metadata for meta file {FileId} from blob storage", file.Id);

                BlobContainerClient containerClient = GetContainerClient();
                string blobName = MetaPrefix + file.Id + MetaFileSuffix;
                BlobClient blobClient = containerClient.GetBlobClient(blobName);

                if (!await blobClient.ExistsAsync(ct))
                {
                    Logger.LogError("Meta file for id {FileId} not found in blob storage", file.Id);
                    throw new FileNotFoundException($"Meta file for id {file.Id} not found in blob storage container.");
                }

                using Stream stream = await blobClient.OpenReadAsync(cancellationToken: ct);
                JsonSerializerOptions options = new() // TODO: Use shared options
                {
                    PropertyNameCaseInsensitive = true
                };
                MatrixMetadata? metadata = await JsonSerializer.DeserializeAsync<MatrixMetadata>(stream, options, ct);
                if (metadata is null)
                {
                    Logger.LogError("Failed to deserialize metadata for id {FileId}", file.Id);
                    throw new InvalidDataException("Failed to deserialize metadata file.");
                }
                return metadata;
            }
        }
        
        private async Task<string[]> GetBinaryFilesAsync(string prefix, string timestamp, CancellationToken ct)
        {
            using (Logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.FUNCTION] = nameof(GetBinaryFilesAsync),
                    [LoggerConsts.CONTAINER_NAME] = ContainerName,
                    ["prefix"] = prefix
                }))
            {
                Logger.LogDebug("Getting binary files from blob storage container with prefix {Prefix}.", prefix);

                List<string> fileNames = [];
                BlobContainerClient containerClient = GetContainerClient();
                AsyncPageable<BlobItem> blobs = containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: ct);

                await foreach (BlobItem blob in blobs)
                {
                    if (blob.Name.Contains(timestamp, StringComparison.OrdinalIgnoreCase))
                    {
                        fileNames.Add(blob.Name);
                    }
                }

                Logger.LogDebug("Found {Count} binary files.", fileNames.Count);
                return [.. fileNames];
            }
        }
    }
}
