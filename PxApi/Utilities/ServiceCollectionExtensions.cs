using PxApi.Configuration;
using PxApi.DataSources;
using PxApi.Models;

namespace PxApi.Utilities
{
    /// <summary>
    /// Factory for creating database connectors based on configuration.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds all configured databases as keyed scoped services.
        /// </summary>
        /// <param name="services">The service collection to add the database connectors to.</param>
        public static void AddDataBaseConnectors(this IServiceCollection services)
        {
            foreach (DataBaseConfig dbConfig in AppSettings.Active.DataBases)
            {
                DataBaseRef db = DataBaseRef.Create(dbConfig.Id);
                switch (dbConfig.Type)
                {
                    case DataBaseType.Mounted:
                        AddMountedConnector(services, dbConfig, db);
                        break;
                    case DataBaseType.FileShare:
                        AddFileShareConnector(services, dbConfig, db);
                        break;
                    case DataBaseType.BlobStorage:
                        AddBlobStorageConnector(services, dbConfig, db);
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported database type: {dbConfig.Type} for database {dbConfig.Id}");
                }
            }
        }

        private static void AddMountedConnector(IServiceCollection services, DataBaseConfig dbConfig, DataBaseRef db)
        {
            services.AddKeyedScoped<IDataBaseConnector>(dbConfig.Id, (serviceProvider, key) =>
            {
                if (!dbConfig.Custom.TryGetValue("RootPath", out string? rootPath) || string.IsNullOrEmpty(rootPath))
                {
                    throw new InvalidOperationException($"Missing required custom configuration value 'RootPath' for database {dbConfig.Id}");
                }

                ILogger<MountedDataBaseConnector> logger = serviceProvider.GetRequiredService<ILogger<MountedDataBaseConnector>>();
                return new MountedDataBaseConnector(db, rootPath, logger);
            });
        }

        private static void AddFileShareConnector(IServiceCollection services, DataBaseConfig dbConfig, DataBaseRef db)
        {
            services.AddKeyedScoped<IDataBaseConnector>(dbConfig.Id, (serviceProvider, key) =>
            {
                if (!dbConfig.Custom.TryGetValue("SharePath", out string? sharePath) || string.IsNullOrEmpty(sharePath))
                {
                    throw new InvalidOperationException($"Missing required custom configuration value 'SharePath' for database {dbConfig.Id}");
                }

                ILogger<FileShareDataBaseConnector> logger = serviceProvider.GetRequiredService<ILogger<FileShareDataBaseConnector>>();
                return new FileShareDataBaseConnector(db, sharePath, logger);
            });
        }

        private static void AddBlobStorageConnector(IServiceCollection services, DataBaseConfig dbConfig, DataBaseRef db)
        {
            services.AddKeyedScoped<IDataBaseConnector>(dbConfig.Id, (serviceProvider, key) =>
            {
                if (!dbConfig.Custom.TryGetValue("ConnectionString", out string? connectionString) || string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException($"Missing required custom configuration value 'ConnectionString' for database {dbConfig.Id}");
                }

                if (!dbConfig.Custom.TryGetValue("ContainerName", out string? containerName) || string.IsNullOrEmpty(containerName))
                {
                    throw new InvalidOperationException($"Missing required custom configuration value 'ContainerName' for database {dbConfig.Id}");
                }

                ILogger<BlobStorageDataBaseConnector> logger = serviceProvider.GetRequiredService<ILogger<BlobStorageDataBaseConnector>>();
                return new BlobStorageDataBaseConnector(db, connectionString, containerName, logger);
            });
        }
    }
}