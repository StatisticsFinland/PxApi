using Px.Utils.ModelBuilders;
using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Metadata.ExtensionMethods;
using Px.Utils.Models.Metadata;
using Px.Utils.PxFile.Data;
using Px.Utils.PxFile.Metadata;
using Px.Utils.PxFile;
using PxApi.DataSources;
using System.Text;
using PxApi.Models;

namespace PxApi.Caching
{
    /// <summary>
    /// Class for reading Px files and extracting metadata and data.
    /// </summary>
    /// <param name="dbConnector">Database connector to read Px files.</param>
    public class PxFileReader(IDataBaseConnector dbConnector)
    {
        /// <summary>
        /// Reads and returns the metadata from a Px file.
        /// </summary>
        /// <param name="file"><see cref="PxFileRef"/> reference to the Px file.</param>
        /// <returns><see cref="IReadOnlyMatrixMetadata"/> object containing the px file metadata.</returns>
        public async Task<IReadOnlyMatrixMetadata> ReadMetadataAsync(PxFileRef file)
        {
            PxFileMetadataReader metaReader = new();
            using Stream stream = await dbConnector.ReadPxFileAsync(file);
            Encoding encoding = await metaReader.GetEncodingAsync(stream);
            if (stream.CanSeek) stream.Seek(0, SeekOrigin.Begin);
            IAsyncEnumerable<KeyValuePair<string, string>> entries = metaReader.ReadMetadataAsync(stream, encoding);
            MatrixMetadataBuilder builder = new();
            return await builder.BuildAsync(entries);
        }

        /// <summary>
        /// Gets the character offset of the start data section in a Px file.
        /// </summary>
        /// <param name="file"><see cref="PxFileRef"/> reference to the Px file.</param>"/>
        /// <returns>Long value representing the offset of the data section</returns>
        public async Task<long?> GetDataSectionOffsetAsync(PxFileRef file)
        {
            using Stream stream = await dbConnector.ReadPxFileAsync(file);
            PxFileConfiguration conf = PxFileConfiguration.Default;
            string dataKey = conf.Tokens.KeyWords.Data;
            long offset = await StreamUtilities.FindKeywordPositionAsync(stream, dataKey, conf);
            return offset + dataKey.Length + 1;
        }

        /// <summary>
        /// Returns the data values from a Px file as an array of <see cref="DoubleDataValue"/>.
        /// </summary>
        /// <param name="file"><see cref="PxFileRef"/> file reference to the Px file.</param>
        /// <param name="dataOffset">Character offset of the data section in the Px file.</param>
        /// <param name="targetMap"><see cref="IMatrixMap"/> representing the metadata structure of the data to read.</param>
        /// <param name="fileMap"><see cref="IMatrixMap"/> representing the complete metadata structure of the Px file.</param>
        /// <returns>Array of <see cref="DoubleDataValue"/> containing the data values.</returns>
        public async Task<DoubleDataValue[]> ReadDataAsync(PxFileRef file, long dataOffset, IMatrixMap targetMap, IMatrixMap fileMap)
        {
            using Stream stream = await dbConnector.ReadPxFileAsync(file);
            using PxFileStreamDataReader dataReader = new(stream, dataOffset);
            DoubleDataValue[] result = new DoubleDataValue[targetMap.GetSize()];
            await dataReader.ReadDoubleDataValuesAsync(result, 0, targetMap, fileMap);
            return result;
        }
    }
}
