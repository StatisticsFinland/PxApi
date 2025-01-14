using Px.Utils.Models.Metadata;

namespace PxApi.DataSources
{
    public interface IDataSource
    {
        public bool IsFile(List<string> hierarchy);

        public Task<IReadOnlyMatrixMetadata> GetTableMetadata(List<string> hierarchy);

        public Task GetDatabases();

        public Task GetContentgroup();
    }
}
