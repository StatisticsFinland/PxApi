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
using System.Globalization;
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

        /// <inheritdoc/>
        protected override bool UseShortFormNames => true;

        private const string MetaPrefix = "meta";
        private const string MetaFileSuffix = ".meta.json";

        private const string DataPrefix = "bin";
        private const string DataFileSuffix = ".pxb";

        private const int DefaultMaxDegreeOfParallelism = 4;

        /// <inheritdoc/>
        public override async Task<PxFileRef[]> GetAllFilesAsync(CancellationToken ct = default)
        {
            using (Logger.BeginScope(new Dictionary<string, object>
            {
                [LoggerConsts.DB_ID] = DataBase.Id,
                [LoggerConsts.FUNCTION] = nameof(GetAllFilesAsync),
                [LoggerConsts.CONTAINER_NAME] = ContainerName
            }))
            {
                ILookup<string, string> metadataBlobs =
                    await GetMetadataBlobsAsync($"{MetaPrefix}/{DataBase.Id}/", null, ct);
                return [.. metadataBlobs.Select(group => PxFileRef.ValidateAndCreate(group.Key, DataBase))];
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
                Logger.LogDebug("Getting last write time for meta file {file_id} from blob storage", file.Id);
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
                                    throw new BinaryBlobSynchronizationException(file, lastUpdated, $"{ContainerName}/{blobName}");
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
                                    Logger.LogDebug("Using streaming read from index {index}.", startIndex);
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
                                    Logger.LogDebug("Window read calls: {count}", windowReaderCallsForDebug);
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
                Logger.LogDebug("Reading metadata for meta file {file_id} from blob storage", file.Id);

                string prefix = BuildMetadataPrefix(file.DataBase.Id, file.Id);
                ILookup<string, string> metadataBlobs = await GetMetadataBlobsAsync(prefix, file.Id, ct);
                string? selectedBlobName = metadataBlobs[file.Id].OrderDescending(StringComparer.Ordinal).FirstOrDefault();
                if (selectedBlobName is null)
                {
                    throw new FileNotFoundException($"Meta file for id {file.Id} not found in database {DataBase.Id}, blob storage container {ContainerName}.");
                }

                using Stream stream = await OpenBlobReadStreamAsync(selectedBlobName, ct);

                string failureMessage = $"Failed to deserialize metadata file {selectedBlobName} for table {file.Id} in database {DataBase.Id}, container {ContainerName}.";
                try
                {
                    MatrixMetadata? metadata = await JsonSerializer.DeserializeAsync<MatrixMetadata>(stream, GlobalJsonConverterOptions.Default, ct);
                    if (metadata is null)
                    {
                        throw new InvalidDataException(failureMessage);
                    }
                    return metadata;
                }
                catch (JsonException ex)
                {
                    throw new InvalidDataException(failureMessage, ex);
                }
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

        private async Task<ILookup<string, string>> GetMetadataBlobsAsync(
            string prefix, string? requestedTableId, CancellationToken ct)
        {
            IReadOnlyList<string> blobNames = await GetBlobItemsAsync(prefix, ct);
            List<(string TableId, string BlobName)> metadataBlobs = [];
            int invalidNames = 0;
            foreach (string blobName in blobNames.Where(name => name.EndsWith(MetaFileSuffix, StringComparison.OrdinalIgnoreCase)))
            {
                ct.ThrowIfCancellationRequested();
                if (!TryParseMetadataBlobName(blobName, out string tableId))
                {
                    invalidNames++;
                    continue;
                }
                if (requestedTableId is not null && !string.Equals(tableId, requestedTableId, StringComparison.Ordinal)) continue;

                metadataBlobs.Add((tableId, blobName));
            }

            if (invalidNames > 0)
            {
                Logger.LogWarning("Skipped {invalid_metadata_file_count} malformed metadata blob names", invalidNames);
            }
            ILookup<string, string> groups = metadataBlobs.ToLookup(blob => blob.TableId, blob => blob.BlobName, StringComparer.Ordinal);
            foreach (IGrouping<string, string> group in groups)
            {
                int versionCount = group.Count();
                if (versionCount > 1)
                {
                    Logger.LogWarning("Multiple meta files for id {file_id} found in blob storage: {version_count}; using latest timestamp",
                        group.Key, versionCount);
                }
            }
            return groups;
        }

        private bool TryParseMetadataBlobName(string blobName, out string tableId)
        {
            tableId = string.Empty;
            string root = $"{MetaPrefix}/{DataBase.Id}/";
            if (!blobName.StartsWith(root, StringComparison.Ordinal)) return false;
            string fileName = blobName[root.Length..^MetaFileSuffix.Length];
            if (fileName.Contains('/')) return false;
            int separator = fileName.LastIndexOf('_');
            if (separator <= 0 || !DateTime.TryParseExact(fileName[(separator + 1)..], "yyyyMMddHHmm",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) return false;

            string candidateId = fileName[..separator];
            try
            {
                PxFileRef file = PxFileRef.ValidateAndCreate(candidateId, DataBase);
                tableId = file.Id;
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
