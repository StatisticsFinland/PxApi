using Px.Utils.Models.Metadata;

namespace PxApi.DataSources
{
    public class LocalFileSystemDataSource : IDataSource
    {
        public Task GetContentgroup(List<string> hierarchy)
        {
            throw new NotImplementedException();
        }

        public Task GetDatabases()
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyMatrixMetadata> GetTableMetadata(List<string> hierarchy)
        {
            throw new NotImplementedException();
        }

        public bool IsFile(List<string> hierarchy)
        {
            throw new NotImplementedException();
        }
    }
}
