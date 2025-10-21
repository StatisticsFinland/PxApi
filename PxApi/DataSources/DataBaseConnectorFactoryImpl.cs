using PxApi.Configuration;
using PxApi.Models;
using PxApi.Utilities;

namespace PxApi.DataSources
{
    /// <summary>
    /// Factory for creating database connectors based on a DataBase instance.
    /// </summary>
    /// <param name="serviceProvider">The service provider to get the database connectors from.</param>
    /// <param name="logger">The logger.</param>
    public class DataBaseConnectorFactoryImpl(IServiceProvider serviceProvider, ILogger<DataBaseConnectorFactoryImpl> logger) : IDataBaseConnectorFactory
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider;
        private readonly ILogger<DataBaseConnectorFactoryImpl> _logger = logger;

        /// <inheritdoc/>
        public IDataBaseConnector GetConnector(DataBaseRef database)
        {
            using (_logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = database.Id,
                    [LoggerConsts.CLASS_NAME] = nameof(DataBaseConnectorFactoryImpl),
                    [LoggerConsts.METHOD_NAME] = nameof(GetConnector)
                }))
            {
                _logger.LogDebug("Getting database connector for database {DatabaseId}", database.Id);
                try
                {
                    return _serviceProvider.GetRequiredKeyedService<IDataBaseConnector>(database.Id);
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogError(ex, "Database connector not found for database {DatabaseId}", database.Id);
                    throw new InvalidOperationException($"Database connector not found for database {database.Id}", ex);
                }
            }
        }

        /// <inheritdoc/>
        public IReadOnlyCollection<DataBaseRef> GetAvailableDatabases()
        {
            using (_logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.CLASS_NAME] = nameof(DataBaseConnectorFactoryImpl),
                    [LoggerConsts.METHOD_NAME] = nameof(GetAvailableDatabases)
                }))
            {
                _logger.LogDebug("Getting list of all available databases");

                List<DataBaseRef> databases = [];

                foreach (DataBaseConfig dbConfig in AppSettings.Active.DataBases)
                {
                    DataBaseRef database = DataBaseRef.Create(dbConfig.Id);
                    databases.Add(database);
                }

                _logger.LogInformation("Found {DatabaseCount} available databases", databases.Count);
                return databases;
            }
        }
    }
}