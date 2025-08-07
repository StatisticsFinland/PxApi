using Px.Utils.ModelBuilders;
using Px.Utils.Models.Metadata;
using Px.Utils.PxFile.Metadata;
using System.Text;

namespace PxApi.UnitTests.Utils
{
    internal static class MatrixMetadataUtils
    {
        internal static async Task<IReadOnlyMatrixMetadata> GetMetadataFromFixture(string fixture)
        {
            PxFileMetadataReader reader = new();
            MemoryStream metadataStream = new(Encoding.UTF8.GetBytes(fixture));
            IAsyncEnumerable<KeyValuePair<string, string>> entries = reader.ReadMetadataAsync(metadataStream, Encoding.UTF8);
            MatrixMetadataBuilder builder = new();
            return await builder.BuildAsync(entries);
        }
    }
}
