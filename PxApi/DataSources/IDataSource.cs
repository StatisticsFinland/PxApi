using Px.Utils.Models.Metadata;

namespace PxApi.DataSources
{
    public interface IDataSource
    {
        public Task<TablePath?> GetTablePathAsync(string database, string filename);

        public Task<IReadOnlyMatrixMetadata> GetTableMetadataAsync(TablePath path);

        public Task GetDatabasesAsync();
    }
}
