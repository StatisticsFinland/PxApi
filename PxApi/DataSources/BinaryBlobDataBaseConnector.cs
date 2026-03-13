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
using PxApi.Configuration;
using PxApi.Exceptions;
using PxApi.Models;
using PxApi.Utilities;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

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
    /// <para>
    /// All direct Azure Blob Storage SDK calls are routed through <c>internal virtual</c> methods
    /// (<see cref="GetBlobItemsAsync"/>, <see cref="BlobExistsAsync"/>, <see cref="OpenBlobReadStreamAsync(string, CancellationToken)"/>,
    /// <see cref="OpenBlobReadStreamAsync(string, long, CancellationToken)"/>, and <see cref="DownloadBlobRangeAsync"/>)
    /// so that tests can subclass and override them without requiring real Azure infrastructure.
    /// </para>
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

                IReadOnlyList<string> blobNames = await GetBlobItemsAsync(MetaPrefix, ct);

                foreach (string blobName in blobNames)
                {
                    string? fileName = TryParseFileIdFromMetaBlobName(blobName);
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
                                if (!await BlobExistsAsync(blobName, ct))
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
                                    return await DownloadBlobRangeAsync(blobName, offset, length, ct);
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
                                        using Stream dataStream = await OpenBlobReadStreamAsync(blobName, HeaderLength + startIndex * reader.ByteCount, ct);

                                        await reader.ReadFromStreamAsync(dataStream, readMap, blobMap, targetMap, result, startIndex, ct);
                                    }
                                    else
                                    {
                                        using Stream blobStream = await OpenBlobReadStreamAsync(blobName, ct);
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

                string prefix = BuildMetadataPrefix(file.DataBase.Id, file.Id);
                IReadOnlyList<string> blobNames = await GetBlobItemsAsync(prefix, ct);
                List<string> metaBlobNames = blobNames
                    .Where(name => name.EndsWith(MetaFileSuffix, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                string? selectedBlobName = null;

                if (metaBlobNames.Count == 0)
                {
                    Logger.LogError("Meta file for id {FileId} not found in blob storage", file.Id);
                    throw new FileNotFoundException($"Meta file for id {file.Id} not found in blob storage container.");
                }
                else if (metaBlobNames.Count > 1)
                {
                    Logger.LogWarning("Multiple meta files for id {FileId} found in blob storage", file.Id);
                    selectedBlobName = metaBlobNames
                        .OrderByDescending(name => name) // Assuming the name includes a timestamp
                        .First();
                }

                selectedBlobName ??= metaBlobNames[0];

                using Stream stream = await OpenBlobReadStreamAsync(selectedBlobName, ct);

                MatrixMetadata? metadata = await JsonSerializer.DeserializeAsync<MatrixMetadata>(stream, GlobalJsonConverterOptions.Default, ct);
                if (metadata is null)
                {
                    Logger.LogError("Failed to deserialize metadata for id {FileId}", file.Id);
                    throw new InvalidDataException("Failed to deserialize metadata file.");
                }
                return metadata;
            }
        }

        /// <summary>
        /// Lists blob names under the given prefix in the configured container.
        /// </summary>
        /// <param name="prefix">The blob name prefix to filter by.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A list of blob names matching the prefix.</returns>
        [ExcludeFromCodeCoverage]
        internal virtual async Task<IReadOnlyList<string>> GetBlobItemsAsync(string prefix, CancellationToken ct = default)
        {
            BlobContainerClient containerClient = GetContainerClient();
            List<string> names = [];
            await foreach (BlobItem blob in containerClient.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix: prefix, cancellationToken: ct))
            {
                names.Add(blob.Name);
            }
            return names;
        }

        /// <summary>
        /// Checks whether a blob with the specified name exists in the configured container.
        /// </summary>
        /// <param name="blobName">The full blob name to check.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns><c>true</c> if the blob exists; otherwise <c>false</c>.</returns>
        [ExcludeFromCodeCoverage]
        internal virtual async Task<bool> BlobExistsAsync(string blobName, CancellationToken ct = default)
        {
            BlobContainerClient containerClient = GetContainerClient();
            BlobClient blob = containerClient.GetBlobClient(blobName);
            return (await blob.ExistsAsync(ct)).Value;
        }

        /// <summary>
        /// Opens a read-only stream for the specified blob from the beginning.
        /// </summary>
        /// <param name="blobName">The full blob name to read.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A readable stream positioned at the start of the blob.</returns>
        [ExcludeFromCodeCoverage]
        internal virtual async Task<Stream> OpenBlobReadStreamAsync(string blobName, CancellationToken ct = default)
        {
            BlobContainerClient containerClient = GetContainerClient();
            BlobClient blob = containerClient.GetBlobClient(blobName);
            return await blob.OpenReadAsync(cancellationToken: ct);
        }

        /// <summary>
        /// Opens a read-only stream for the specified blob starting at the given byte position.
        /// </summary>
        /// <param name="blobName">The full blob name to read.</param>
        /// <param name="position">The byte offset at which to start reading.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A readable stream positioned at <paramref name="position"/>.</returns>
        [ExcludeFromCodeCoverage]
        internal virtual async Task<Stream> OpenBlobReadStreamAsync(string blobName, long position, CancellationToken ct = default)
        {
            BlobContainerClient containerClient = GetContainerClient();
            BlobClient blob = containerClient.GetBlobClient(blobName);
            return await blob.OpenReadAsync(new BlobOpenReadOptions(allowModifications: false)
            {
                Position = position
            }, cancellationToken: ct);
        }

        /// <summary>
        /// Downloads a specific byte range from a blob as a stream.
        /// </summary>
        /// <param name="blobName">The full blob name to read from.</param>
        /// <param name="offset">The byte offset to start reading from.</param>
        /// <param name="length">The number of bytes to read.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A stream containing the requested byte range.</returns>
        [ExcludeFromCodeCoverage]
        internal virtual async Task<Stream> DownloadBlobRangeAsync(string blobName, long offset, long length, CancellationToken ct = default)
        {
            BlobContainerClient containerClient = GetContainerClient();
            BlobClient blob = containerClient.GetBlobClient(blobName);
            Response<BlobDownloadStreamingResult> result = await blob.DownloadStreamingAsync(new HttpRange(offset, length), null, false, ct);
            return result.Value.Content;
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
