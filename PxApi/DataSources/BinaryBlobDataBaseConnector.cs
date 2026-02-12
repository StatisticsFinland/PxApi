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
    /// <summary>
    /// Blob-backed database connector for PX binary data (Pxb) and associated metadata stored in Azure Blob Storage.
    /// </summary>
    /// <remarks>
    /// This connector:
    /// <list type="bullet">
    /// <item><description>Lists available PX files by enumerating metadata blobs under <c>meta/</c>.</description></item>
    /// <item><description>Reads metadata from <c>*.meta.json</c> blobs and deserializes it into <see cref="MatrixMetadata"/>.</description></item>
    /// <item><description>Reads binary data from <c>*.pxb</c> blobs under <c>bin/</c>, optionally using windowed reads for dense selections.</description></item>
    /// </list>
    /// Read strategy selection for binary data is delegated to <see cref="BlobReadModeSelector"/>.
    /// </remarks>
    /// <param name="dataBase">The database reference used to construct blob paths and logging scope values.</param>
    /// <param name="containerName">The Azure blob storage container name hosting metadata and binary blobs.</param>
    /// <param name="blobServiceClientFactory">Factory for creating <see cref="BlobServiceClient"/> instances.</param>
    /// <param name="logger">Logger used for scoped diagnostic output.</param>
    public class BinaryBlobDataBaseConnector(DataBaseRef dataBase, string containerName, IAzureClientFactory<BlobServiceClient> blobServiceClientFactory, ILogger<BinaryBlobDataBaseConnector> logger)
        : BlobDataBaseConnector(dataBase, containerName, blobServiceClientFactory)
    {
        /// <inheritdoc/>
        protected override ILogger Logger => logger;

        private const string MetaPrefix = "meta";
        private const string MetaFileSuffix = ".meta.json";

        private const string DataPrefix = "bin";
        private const string DataFileSuffix = ".pxb";

        private const int DefaultMaxDegreeOfParallelism = 4;

        /// <inheritdoc/>
        public override async Task<string[]> GetAllFilesAsync(CancellationToken ct = default)
        {
            using (Logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
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
                    string? fileName = TryParseFileIdFromMetaBlobName(blob.Name);
                    if (fileName is not null)
                    {
                        fileNames.Add(fileName);
                    }
                }

                Logger.LogDebug("Found {Count} meta files.", fileNames.Count);
                return [.. fileNames];
            }
        }

        /// <inheritdoc/>
        public override async Task<DateTime> GetLastWriteTimeAsync(PxFileRef file, CancellationToken ct = default)
        {
            using (Logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
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
        public async override Task<DoubleDataValue[]> ReadDataAsync(PxFileRef file, IMatrixMap targetMap, IReadOnlyMatrixMetadata fileMeta, CancellationToken ct = default)
        {
            using (Logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.FUNCTION] = nameof(ReadDataAsync),
                    [LoggerConsts.PX_FILE] = file.Id,
                    [LoggerConsts.CONTAINER_NAME] = ContainerName
                }))
            {
                Logger.LogDebug("Reading data from binary files.");
                ContentDimension contentDimension = fileMeta.GetContentDimension();
                IReadOnlyList<string> contentDimensionCodes = targetMap.DimensionMaps
                    .First(dimMap => dimMap.Code == contentDimension.Code).ValueCodes;

                DateTime lastUpdated = contentDimension.Values.Map(value => value.LastUpdated).Max();
                string timestamp = lastUpdated.ToString("yyyyMMddHHmm");
                DoubleDataValue[] result = new DoubleDataValue[targetMap.GetSize()];

                int maxDegreeOfParallelism = DefaultMaxDegreeOfParallelism;
                using SemaphoreSlim throttler = new(maxDegreeOfParallelism, maxDegreeOfParallelism);

                Task[] tasks = contentDimensionCodes
                    .Select(async cValCode =>
                    {
                        await throttler.WaitAsync(ct);
                        try
                        {
                            ct.ThrowIfCancellationRequested();
                            string blobName = BuildDataBlobName(file.DataBase.Id, file.Id, cValCode, timestamp);

                            using (Logger.BeginScope(
                                new Dictionary<string, object>
                                {
                                    [LoggerConsts.CONTENT_VALUE_CODE] = cValCode,
                                    [LoggerConsts.BLOB_NAME] = blobName
                                }))
                            {
                                BlobContainerClient containerClient = GetContainerClient();
                                BlobClient blob = containerClient.GetBlobClient(blobName);
                                if (!await blob.ExistsAsync(ct))
                                {
                                    Logger.LogError("Data blob {BlobName} not found in blob storage.", blobName);
                                    throw new BinaryBlobSynchronizationException(file, lastUpdated);
                                }

                                IMatrixMap readMap = targetMap.CollapseDimension(contentDimension.Code, cValCode);
                                IMatrixMap blobMap = fileMeta.CollapseDimension(contentDimension.Code, cValCode);

                                int windowReaderCallsForDebug = 0;
                                async Task<Stream> readerFunc(long offset, long length, CancellationToken ct)
                                {
                                    Interlocked.Increment(ref windowReaderCallsForDebug);
                                    Response<BlobDownloadStreamingResult> result = await blob.DownloadStreamingAsync(new HttpRange(offset, length), null, false, ct);
                                    return result.Value.Content;
                                }

                                if (BlobReadModeSelector.ReadStreaming(readMap, blobMap, out long startIndex))
                                {
                                    Logger.LogDebug("Using streaming read from index {Index}.", startIndex);
                                    if (startIndex > 0)
                                    {
                                        byte[] headerBytes = new byte[8];
                                        using Stream headerStream = await readerFunc(0, 8, ct);
                                        await headerStream.ReadExactlyAsync(headerBytes, ct);

                                        (uint HeaderLength, BinaryValueCodecType Codec) = ParsePxbHeader(headerBytes);

                                        BinaryDataReader reader = BinaryDataReader.Create(Codec, headerLengthBytes: HeaderLength);
                                        using Stream dataStream = await blob.OpenReadAsync(new BlobOpenReadOptions(allowModifications: false)
                                        {
                                            Position = HeaderLength + startIndex * reader.ByteCount
                                        }, cancellationToken: ct);

                                        await reader.ReadFromStreamAsync(dataStream, readMap, blobMap, targetMap, result, startIndex, ct);
                                    }
                                    else
                                    {
                                        using Stream blobStream = await blob.OpenReadAsync(cancellationToken: ct);
                                        byte[] headerBytes = new byte[8];
                                        await blobStream.ReadExactlyAsync(headerBytes, ct);
                                        (uint _, BinaryValueCodecType Codec) = ParsePxbHeader(headerBytes); // We already read the header
                                        BinaryDataReader reader = BinaryDataReader.Create(Codec);

                                        await reader.ReadFromStreamAsync(blobStream, readMap, blobMap, targetMap, result, ct);
                                    }
                                }
                                else
                                {
                                    Logger.LogDebug("Using windowed read.");
                                    byte[] headerBytes = new byte[8];
                                    using Stream headerStream = await readerFunc(0, 8, ct);
                                    await headerStream.ReadExactlyAsync(headerBytes, ct);

                                    (uint HeaderLength, BinaryValueCodecType Codec) = ParsePxbHeader(headerBytes);

                                    BinaryDataReader reader = BinaryDataReader.Create(Codec, headerLengthBytes: HeaderLength);
                                    await reader.ReadByChunkAsync(readerFunc, readMap, blobMap, targetMap, result, ct);
                                    Logger.LogDebug("Window read calls: {Count}", windowReaderCallsForDebug);
                                }
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
        }

        /// <inheritdoc/>
        public override async Task<IReadOnlyMatrixMetadata> ReadMetadataAsync(PxFileRef file, CancellationToken ct = default)
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
                string prefix = BuildMetadataPrefix(file.DataBase.Id, file.Id);
                List<BlobItem> blobs = await containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: ct)
                    .Where(blob => blob.Name.EndsWith(MetaFileSuffix, StringComparison.OrdinalIgnoreCase))
                    .ToListAsync(ct);

                BlobItem? blobItem = null;

                if (blobs.Count == 0)
                {
                    Logger.LogError("Meta file for id {FileId} not found in blob storage", file.Id);
                    throw new FileNotFoundException($"Meta file for id {file.Id} not found in blob storage container.");
                }
                else if (blobs.Count > 1)
                {
                    Logger.LogWarning("Multiple meta files for id {FileId} found in blob storage", file.Id);
                    blobItem = blobs
                        .OrderByDescending(blob => blob.Name) // Assuming the name includes a timestamp
                        .First();
                }

                blobItem ??= blobs[0];

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

        internal static string? TryParseFileIdFromMetaBlobName(string blobName)
        {
            if (!blobName.StartsWith(MetaPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!blobName.EndsWith(MetaFileSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string withoutPrefixAndSuffix = blobName[(MetaPrefix.Length + 1)..^MetaFileSuffix.Length];
            string candidate = withoutPrefixAndSuffix.Split('_')[0];
            return string.IsNullOrWhiteSpace(candidate) ? null : candidate;
        }

        internal static string BuildMetadataPrefix(string dbId, string fileId)
        {
            return $"{MetaPrefix}/{dbId}/{fileId}_";
        }

        internal static string BuildDataBlobName(string dbId, string fileId, string contentValueCode, string timestamp)
        {
            return $"{DataPrefix}/{dbId}/{fileId}_{contentValueCode}_{timestamp}{DataFileSuffix}";
        }

        internal static string GetTimestamp(ContentValueList values)
        {
            DateTime timestamp = values.Map(value => value.LastUpdated).Max();
            return timestamp.ToString("yyyyMMddHHmm");
        }

        internal static (uint HeaderLength, BinaryValueCodecType Codec) ParsePxbHeader(ReadOnlySpan<byte> headerBytes)
        {
            if (headerBytes.Length < 8)
            {
                throw new ArgumentException("Header must be at least 8 bytes.", nameof(headerBytes));
            }

            uint headerLength = BitConverter.ToUInt32(headerBytes[..4]);
            uint codecRaw = BitConverter.ToUInt32(headerBytes.Slice(4, 4));
            BinaryValueCodecType codec = (BinaryValueCodecType)codecRaw;

            return (headerLength, codec);
        }

    }
}
