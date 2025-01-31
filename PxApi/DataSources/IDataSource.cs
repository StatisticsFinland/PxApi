using Px.Utils.Models.Metadata;

namespace PxApi.DataSources
{
    /// <summary>
    /// Interface for abstracting the data source.
    /// </summary>
    public interface IDataSource
    {
        /// <summary>
        /// Get <see cref="TablePath"/> object for accessing the table."/>
        /// </summary>
        /// <param name="database">The database that contains the table.</param>
        /// <param name="filename">Name of the table table.</param>
        /// <returns></returns>
        public Task<TablePath?> GetTablePathAsync(string database, string filename);

        /// <summary>
        /// Returns the metadata for the table.
        /// </summary>
        /// <param name="path"><see cref="TablePath"/> object for accessing the table.</param>
        /// <returns><see cref="IReadOnlyMatrixMetadata"/> containing the metadata of the whole table.</returns>
        public Task<IReadOnlyMatrixMetadata> GetTableMetadataAsync(TablePath path);

        /// <summary>
        /// Get the list of databases available in the data source.
        /// </summary>
        /// <returns></returns>
        public Task GetDatabasesAsync();
    }
}
