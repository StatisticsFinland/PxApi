using System.Text;
using Px.Utils.ModelBuilders;
using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Metadata;
using Px.Utils.Models.Metadata.ExtensionMethods;
using Px.Utils.PxFile.Data;
using Px.Utils.PxFile.Metadata;
using PxApi.Models;

namespace PxApi.DataSources
{
    /// <summary>
    /// Base class for database connectors that can read PX content via a stream.
    /// Provides shared implementations for reading metadata and data from PX files.
    /// </summary>
    public abstract class DataBaseConnector(DataBaseRef dataBase) : IDataBaseConnector
    {
        /// <inheritdoc/>
        public DataBaseRef DataBase => dataBase;

        /// <summary>
        /// Internal logger for the connector.
        /// </summary>
        protected abstract ILogger Logger { get; }

        /// <inheritdoc/>
        public abstract Task<PxFileRef[]> GetAllFilesAsync(CancellationToken ct = default);

        /// <inheritdoc/>
        public abstract Task<DateTime> GetLastWriteTimeAsync(PxFileRef file, CancellationToken ct = default);

        /// <inheritdoc/>
        public abstract Task<Stream> TryReadAuxiliaryFileAsync(string fileName, string[]? hierarchy, CancellationToken ct = default);

        /// <summary>
        /// Opens a stream for the given PX file.
        /// </summary>
        /// <param name="file">The PX file reference.</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>A stream for reading the PX file contents.</returns>
        protected abstract Task<Stream> OpenPxFileStreamAsync(PxFileRef file, CancellationToken ct = default);

        /// <inheritdoc/>
        public virtual async Task<IReadOnlyMatrixMetadata> ReadMetadataAsync(PxFileRef file, CancellationToken ct = default)
        {
            PxFileMetadataReader metaReader = new();
            Encoding encoding;

            using (Stream encodingStream = await OpenPxFileStreamAsync(file, ct))
            {
                encoding = await metaReader.GetEncodingAsync(encodingStream, ct);
                if (encodingStream.CanSeek)
                {
                    encodingStream.Seek(0, SeekOrigin.Begin);
                    IAsyncEnumerable<KeyValuePair<string, string>> entries = metaReader.ReadMetadataAsync(encodingStream, encoding, cancellationToken: ct);
                    MatrixMetadataBuilder builder = new();
                    return await builder.BuildAsync(entries);
                }
            }

            using Stream metadataStream = await OpenPxFileStreamAsync(file, ct);
            IAsyncEnumerable<KeyValuePair<string, string>> metadataEntries = metaReader.ReadMetadataAsync(metadataStream, encoding, cancellationToken: ct);
            MatrixMetadataBuilder metadataBuilder = new();
            return await metadataBuilder.BuildAsync(metadataEntries);
        }

        /// <inheritdoc/>
        public virtual async Task<DoubleDataValue[]> ReadDataAsync(PxFileRef file, IMatrixMap targetMap, IReadOnlyMatrixMetadata fileMeta, CancellationToken ct = default)
        {
            using Stream stream = await OpenPxFileStreamAsync(file, ct);
            using PxFileStreamDataReader dataReader = new(stream);
            DoubleDataValue[] result = new DoubleDataValue[targetMap.GetSize()];
            await dataReader.ReadDoubleDataValuesAsync(result, 0, targetMap, fileMeta, ct);
            return result;
        }

        /// <inheritdoc/>
        public virtual async Task<string> GetSingleRawMetadataValueAsync(string key, PxFileRef file, CancellationToken ct = default)
        {
            PxFileMetadataReader reader = new();
            Encoding encoding;

            using (Stream probeStream = await OpenPxFileStreamAsync(file, ct))
            {
                encoding = await reader.GetEncodingAsync(probeStream, ct);

                if (probeStream.CanSeek)
                {
                    probeStream.Seek(0, SeekOrigin.Begin);
                    IAsyncEnumerable<KeyValuePair<string, string>> metaEntries = reader.ReadMetadataAsync(probeStream, encoding, cancellationToken: ct);
                    await foreach (KeyValuePair<string, string> pair in metaEntries)
                    {
                        if (pair.Key == key)
                        {
                            return pair.Value;
                        }
                    }

                    throw new InvalidOperationException($"Key '{key}' not found in metadata");
                }
            }

            using Stream fileStream = await OpenPxFileStreamAsync(file, ct);
            IAsyncEnumerable<KeyValuePair<string, string>> freshMetaEntries = reader.ReadMetadataAsync(fileStream, encoding, cancellationToken: ct);
            await foreach (KeyValuePair<string, string> pair in freshMetaEntries)
            {
                if (pair.Key == key)
                {
                    return pair.Value;
                }
            }

            throw new InvalidOperationException($"Key '{key}' not found in metadata");
        }
    }
}
