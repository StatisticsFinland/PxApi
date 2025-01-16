using Px.Utils.ModelBuilders;
using Px.Utils.Models.Metadata;
using Px.Utils.PxFile.Metadata;
using PxApi.Configuration;
using PxApi.Utilities;
using System.Text;

namespace PxApi.DataSources
{
    public class LocalFileSystemDataSource() : IDataSource
    {

        private readonly LocalFileSystemConfig config = AppSettings.Active.DataSource.LocalFileSystem;

        public Task GetContentgroupAsync(List<string> hierarchy)
        {
            throw new NotImplementedException();
        }

        public Task GetDatabasesAsync()
        {
            throw new NotImplementedException();
        }

        public async Task<IReadOnlyMatrixMetadata> GetTableMetadataAsync(List<string> hierarchy)
        {
            string path = PathFunctions.BuildAndSecurePath(config.RootPath, hierarchy);

            PxFileMetadataReader reader = new();
            using FileStream fileStream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            Encoding encoding = await reader.GetEncodingAsync(fileStream);

            if (fileStream.CanSeek) fileStream.Seek(0, SeekOrigin.Begin);
            else throw new InvalidOperationException("Not able to seek in the filestream");

            IAsyncEnumerable<KeyValuePair<string, string>> metaEntries = reader.ReadMetadataAsync(fileStream, encoding);
            
            MatrixMetadataBuilder builder = new();
            return await builder.BuildAsync(metaEntries);
        }

        public Task<bool> IsFileAsync(List<string> hierarchy)
        {
            string path = PathFunctions.BuildAndSecurePath(config.RootPath, hierarchy);
            return Task.Factory.StartNew(() => File.Exists(path));
        }
    }
}
