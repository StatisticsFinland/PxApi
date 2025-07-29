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
    public class PxfileReader(IDataBaseConnector fileSystem)
    {
        public async Task<IReadOnlyMatrixMetadata> ReadMetadata(PxFileRef file)
        {
            PxFileMetadataReader metaReader = new();
            using Stream stream = fileSystem.ReadPxFile(file);
            Encoding encoding = await metaReader.GetEncodingAsync(stream);
            if (stream.CanSeek) stream.Seek(0, SeekOrigin.Begin);
            IAsyncEnumerable<KeyValuePair<string, string>> entries = metaReader.ReadMetadataAsync(stream, encoding);
            MatrixMetadataBuilder builder = new();
            return await builder.BuildAsync(entries);
        }

        public async Task<long?> GetDataSectionOffset(PxFileRef file)
        {
            using Stream stream = fileSystem.ReadPxFile(file);
            PxFileConfiguration conf = PxFileConfiguration.Default;
            string dataKey = conf.Tokens.KeyWords.Data;
            long offset = await StreamUtilities.FindKeywordPositionAsync(stream, dataKey, conf);
            return offset + dataKey.Length + 1;
        }

        public async Task<DoubleDataValue[]> ReadDataAsync(PxFileRef file, long dataOffset, IMatrixMap targetMap, IMatrixMap fileMap)
        {
            using Stream stream = fileSystem.ReadPxFile(file);
            using PxFileStreamDataReader dataReader = new(stream);
            DoubleDataValue[] result = new DoubleDataValue[targetMap.GetSize()];
            await dataReader.ReadDoubleDataValuesAsync(result, 0, targetMap, fileMap);
            return result;
        }
    }
}
