using Microsoft.Extensions.DependencyInjection;
using PxApi.Configuration;
using PxApi.Models;
using PxApi.Utilities;

namespace PxApi.DataSources
{
    /// <summary>
    /// Factory for creating database connectors based on a DataBase instance.
    /// </summary>
    public class DataBaseConnectorFactoryImpl : IDataBaseConnectorFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DataBaseConnectorFactoryImpl> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DataBaseConnectorFactoryImpl"/> class.
        /// </summary>
        /// <param name="serviceProvider">The service provider to get the database connectors from.</param>
        /// <param name="logger">The logger.</param>
        public DataBaseConnectorFactoryImpl(IServiceProvider serviceProvider, ILogger<DataBaseConnectorFactoryImpl> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

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

                try
                {
                    List<DataBaseRef> databases = new List<DataBaseRef>();

                    foreach (DataBaseConfig dbConfig in AppSettings.Active.DataBases)
                    {
                        DataBaseRef database = DataBaseRef.Create(dbConfig.Id);
                        databases.Add(database);
                    }

                    _logger.LogInformation("Found {DatabaseCount} available databases", databases.Count);
                    return databases;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to retrieve available databases");
                    throw;
                }
            }
        }
    }
}