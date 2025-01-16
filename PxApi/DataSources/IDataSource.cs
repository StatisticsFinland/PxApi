using Px.Utils.Models.Metadata;

namespace PxApi.DataSources
{
    public interface IDataSource
    {
        public Task<bool> IsFileAsync(List<string> hierarchy);

        public Task<IReadOnlyMatrixMetadata> GetTableMetadataAsync(List<string> hierarchy);

        public Task GetDatabasesAsync();

        public Task GetContentgroupAsync(List<string> hierarchy);
    }
}
