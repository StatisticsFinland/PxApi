using PxApi.Models;

namespace PxApi.DataSources
{
    /// <summary>
    /// Factory for creating database connectors based on a DataBase instance.
    /// </summary>
    public interface IDataBaseConnectorFactory
    {
        /// <summary>
        /// Gets a database connector for the specified database.
        /// </summary>
        /// <param name="database">The database to get the connector for.</param>
        /// <returns>The database connector for the specified database.</returns>
        IDataBaseConnector GetConnector(DataBaseRef database);

        /// <summary>
        /// Gets a list of all available databases.
        /// </summary>
        /// <returns>A read-only collection of all available databases.</returns>
        IReadOnlyCollection<DataBaseRef> GetAvailableDatabases();
    }
}