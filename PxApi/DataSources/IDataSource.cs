using Px.Utils.Models.Metadata;
using PxApi.Models;
using System.Collections.Immutable;

namespace PxApi.DataSources
{
    /// <summary>
    /// Interface for abstracting the data source.
    /// </summary>
    public interface IDataSource
    {
        /// <summary>
        /// Get <see cref="PxTable"/> object for accessing the table."/>
        /// </summary>
        /// <param name="database">The database that contains the table.</param>
        /// <param name="filename">Name of the table table.</param>
        public Task<PxTable> GetTablePathAsync(string database, string filename);

        /// <summary>
        /// Get the sorted dictionary of tables in the database.
        /// The dictionary key is the filename of the table, and the value is the <see cref="PxTable"/> object for accessing the table.
        /// The dictionary is sorted by the filename.
        /// </summary>
        /// <param name="dbId">Name of the database to list the tables from.</param>
        public Task<ImmutableSortedDictionary<string, PxTable>> GetSortedTableDictCachedAsync(string dbId);

        /// <summary>
        /// Returns the metadata for the table.
        /// </summary>
        /// <param name="path"><see cref="PxTable"/> object for accessing the table.</param>
        /// <returns><see cref="IReadOnlyMatrixMetadata"/> containing the metadata of the whole table.</returns>
        public Task<IReadOnlyMatrixMetadata> GetMatrixMetadataCachedAsync(PxTable path);

        /// <summary>
        /// Get a single string value from a table.
        /// Used to read meta entries if the metadata does now allow for constructing a matrix metadata object.
        /// </summary>
        /// <param name="key">Key that contains all spesifiers and language code if needed.</param>
        /// <param name="path">Path to the table.</param>
        public Task<string> GetSingleStringValueFromTable(string key, PxTable path);

        /// <summary>
        /// Get all the groupings for a table.
        /// </summary>
        public Task<List<TableGroup>> GetTableGroupingCachedAsync(PxTable table, string lang);


        /// <summary>
        /// Get the list of databases available in the data source.
        /// </summary>
        public Task GetDatabasesAsync();
    }
}
