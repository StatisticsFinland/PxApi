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
        public abstract Task<string[]> GetAllFilesAsync();

        /// <inheritdoc/>
        public abstract Task<DateTime> GetLastWriteTimeAsync(PxFileRef file);

        /// <inheritdoc/>
        public abstract Task<Stream> TryReadAuxiliaryFileAsync(string relativePath);

        /// <summary>
        /// Opens a readable stream to the PX file. Implement in connectors that have stream access.
        /// </summary>
        /// <param name="file">Reference to the PX file.</param>
        /// <returns>Open readable stream.</returns>
        protected virtual Task<Stream> OpenPxFileStreamAsync(PxFileRef file)
        {
            throw new NotSupportedException("This connector does not support direct stream access to PX files.");
        }

        /// <inheritdoc/>
        public virtual async Task<IReadOnlyMatrixMetadata> ReadMetadataAsync(PxFileRef file)
        {
            PxFileMetadataReader metaReader = new();
            using Stream stream = await OpenPxFileStreamAsync(file);
            Encoding encoding = await metaReader.GetEncodingAsync(stream);
            if (stream.CanSeek) stream.Seek(0, SeekOrigin.Begin);
            IAsyncEnumerable<KeyValuePair<string, string>> entries = metaReader.ReadMetadataAsync(stream, encoding);
            MatrixMetadataBuilder builder = new();
            return await builder.BuildAsync(entries);
        }

        /// <inheritdoc/>
        public virtual async Task<DoubleDataValue[]> ReadDataAsync(PxFileRef file, IMatrixMap targetMap, IReadOnlyMatrixMetadata fileMeta)
        {
            using Stream stream = await OpenPxFileStreamAsync(file);
            using PxFileStreamDataReader dataReader = new(stream);
            DoubleDataValue[] result = new DoubleDataValue[targetMap.GetSize()];
            await dataReader.ReadDoubleDataValuesAsync(result, 0, targetMap, fileMeta);
            return result;
        }

        /// <inheritdoc/>
        public virtual async Task<string> GetSingleRawMetadataValueAsync(string key, PxFileRef file)
        {
            using Stream fileStream = await OpenPxFileStreamAsync(file);
            PxFileMetadataReader reader = new();
            Encoding encoding = await reader.GetEncodingAsync(fileStream);

            if (fileStream.CanSeek) fileStream.Seek(0, SeekOrigin.Begin);
            else throw new InvalidOperationException("Not able to seek in the filestream");

            IAsyncEnumerable<KeyValuePair<string, string>> metaEntries = reader.ReadMetadataAsync(fileStream, encoding);
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
}
