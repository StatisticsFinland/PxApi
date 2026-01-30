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
using PxApi.Configuration;

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

        private const int DefaultMaxDegreeOfParallelism = 4;

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
                        // Take only the file identifier without prefix (path) and suffix (timestamp)
                        string fileName = blob.Name[(MetaPrefix.Length)..^MetaFileSuffix.Length]
                            .Split('_')[0]; // Split to remove timestamp if any
                        fileNames.Add(fileName);
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
                IReadOnlyMatrixMetadata metadata = await ReadMetadataAsync(file, ct);
                ContentValueList contentDimensionValues = metadata.GetContentDimension().Values;
                return contentDimensionValues.Map(value => value.LastUpdated).Max();
            }
        }

        /// <inheritdoc/>
        public async override Task<DoubleDataValue[]> ReadDataAsync(PxFileRef file, IMatrixMap targetMap, IReadOnlyMatrixMetadata meta, CancellationToken ct)
        {
            ContentDimension contentDimension = meta.GetContentDimension();
            string timestamp = GetTimestamp(contentDimension.Values);
            DoubleDataValue[] result = new DoubleDataValue[targetMap.GetSize()];

            int maxDegreeOfParallelism = DefaultMaxDegreeOfParallelism;
            using SemaphoreSlim throttler = new(maxDegreeOfParallelism, maxDegreeOfParallelism);

            Task[] tasks = contentDimension.Values
                .Select(val => val.Code)
                .Select(async (string cValCode) =>
                {
                    await throttler.WaitAsync(ct);
                    try
                    {
                        ct.ThrowIfCancellationRequested();
                        string blobName = $"{DataPrefix}{file.DataBase.Id}/{file.Id}_{cValCode}_{timestamp}{DataFileSuffix}";
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

                        async Task<Stream> readerFunc(long offset, long length, CancellationToken ct)
                        {
                            Response<BlobDownloadStreamingResult> result = await blob.DownloadStreamingAsync(new HttpRange(offset, length), null, false, ct);
                            return result.Value.Content;
                        }

                        if (BlobReadModeSelector.ReadStreaming(readMap, blobMap, out long startIndex))
                        {
                            if (startIndex > 0)
                            {
                                byte[] headerBytes = new byte[8];
                                using Stream headerStream = await readerFunc(0, 8, ct);
                                await headerStream.ReadExactlyAsync(headerBytes, ct);

                                uint headerLength = BitConverter.ToUInt32(headerBytes, 0);
                                BinaryValueCodecType codec = (BinaryValueCodecType)BitConverter.ToUInt32(headerBytes, 4);

                                BinaryDataReader reader = BinaryDataReader.Create(codec, headerLengthBytes: headerLength);
                                using Stream dataStream = await blob.OpenReadAsync(new BlobOpenReadOptions(allowModifications: false)
                                {
                                }, cancellationToken: ct);

                                await reader.ReadFromStreamAsync(headerStream, readMap, blobMap, targetMap, result, startIndex, ct);
                            }
                            else
                            {
                                using Stream blobStream = await blob.OpenReadAsync(cancellationToken: ct);
                                byte[] headerBytes = new byte[8];
                                await blobStream.ReadExactlyAsync(headerBytes, ct);
                                BinaryValueCodecType codec = (BinaryValueCodecType)BitConverter.ToUInt32(headerBytes, 4);
                                BinaryDataReader reader = BinaryDataReader.Create(codec);

                                await reader.ReadFromStreamAsync(blobStream, readMap, blobMap, targetMap, result, ct);
                            }
                        }
                        else
                        {
                            byte[] headerBytes = new byte[8];
                            using Stream headerStream = await readerFunc(0, 8, ct);
                            await headerStream.ReadExactlyAsync(headerBytes, ct);

                            uint headerLength = BitConverter.ToUInt32(headerBytes, 0);
                            BinaryValueCodecType codec = (BinaryValueCodecType)BitConverter.ToUInt32(headerBytes, 4);

                            BinaryDataReader reader = BinaryDataReader.Create(codec, headerLengthBytes: headerLength);
                            await reader.ReadByChunkAsync(readerFunc, readMap, blobMap, targetMap, result, ct);
                        }
                    }
                    finally
                    {
                        throttler.Release();
                    }
                })
                .ToArray();

            await Task.WhenAll(tasks);
            return result;
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
                string prefix = $"{MetaPrefix}{file.DataBase.Id}/{file.Id}_";
                IAsyncEnumerable<BlobItem> blobs = containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: ct)
                    .Where(blob => blob.Name.EndsWith(MetaFileSuffix, StringComparison.OrdinalIgnoreCase));

                BlobItem? blobItem = null;

                if (!await blobs.AnyAsync(ct))
                {
                    Logger.LogError("Meta file for id {FileId} not found in blob storage", file.Id);
                    throw new FileNotFoundException($"Meta file for id {file.Id} not found in blob storage container.");
                }
                else if (await blobs.CountAsync(ct) > 1)
                {
                    Logger.LogWarning("Multiple meta files for id {FileId} found in blob storage", file.Id);
                    blobItem = await blobs
                        .OrderByDescending(blob => blob.Properties.LastModified)
                        .FirstAsync(ct);
                }
                
                blobItem ??= await blobs.FirstAsync(ct);

                BlobClient blobClient = containerClient.GetBlobClient(blobItem.Name);
                using Stream stream = await blobClient.OpenReadAsync(cancellationToken: ct);
                
                MatrixMetadata? metadata = await JsonSerializer.DeserializeAsync<MatrixMetadata>(stream, GlobalJsonConverterOptions.Default, ct);
                if (metadata is null)
                {
                    Logger.LogError("Failed to deserialize metadata for id {FileId}", file.Id);
                    throw new InvalidDataException("Failed to deserialize metadata file.");
                }
                return metadata;
            }
        }

        private static string GetTimestamp(ContentValueList values)
        {
            DateTime timestamp = values.Map(value => value.LastUpdated).Max();
            return timestamp.ToString("yyyyMMddHHmm");
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
