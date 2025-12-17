using PxApi.Configuration;
using PxApi.DataSources;
using PxApi.Models;
using Microsoft.Extensions.Azure;
using Azure.Identity;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using Azure.Storage.Blobs;

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
            if (!dbConfig.Custom.TryGetValue("StoragePath", out string? storagePath) || string.IsNullOrEmpty(storagePath))
            {
                throw new InvalidOperationException($"Missing required custom configuration value 'StoragePath' for database {dbConfig.Id}");
            }

            if (!dbConfig.Custom.TryGetValue("ShareName", out string? shareName) || string.IsNullOrEmpty(shareName))
            {
                throw new InvalidOperationException($"Missing required custom configuration value 'ShareName' for database {dbConfig.Id}");
            }

            // Register a named ShareServiceClient for this database using DefaultAzureCredential and ShareTokenIntent
            services.AddAzureClients(clientBuilder =>
            {
                clientBuilder.UseCredential(new DefaultAzureCredential());
                Uri storageUri = new(storagePath);

                clientBuilder
                    .AddClient<ShareServiceClient, ShareClientOptions>((options, credential, sp) => new ShareServiceClient(storageUri, credential, options))
                    .ConfigureOptions(o => o.ShareTokenIntent = ShareTokenIntent.Backup)
                    .WithName(dbConfig.Id);
            });

            services.AddKeyedScoped<IDataBaseConnector>(dbConfig.Id, (serviceProvider, key) =>
            {
                ILogger<FileShareDataBaseConnector> logger = serviceProvider.GetRequiredService<ILogger<FileShareDataBaseConnector>>();
                IAzureClientFactory<ShareServiceClient> factory = serviceProvider.GetRequiredService<IAzureClientFactory<ShareServiceClient>>();
                return new FileShareDataBaseConnector(db, shareName, factory, logger);
            });
        }

        private static void AddBlobStorageConnector(IServiceCollection services, DataBaseConfig dbConfig, DataBaseRef db)
        {
            if (!dbConfig.Custom.TryGetValue("ConnectionString", out string? connectionString) || string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException($"Missing required custom configuration value 'ConnectionString' for database {dbConfig.Id}");
            }

            if (!dbConfig.Custom.TryGetValue("ContainerName", out string? containerName) || string.IsNullOrEmpty(containerName))
            {
                throw new InvalidOperationException($"Missing required custom configuration value 'ContainerName' for database {dbConfig.Id}");
            }

            // Register a named BlobServiceClient for this database
            services.AddAzureClients(clientBuilder =>
            {
                clientBuilder
                    .AddBlobServiceClient(connectionString)
                    .WithName(dbConfig.Id);
            });

            services.AddKeyedScoped<IDataBaseConnector>(dbConfig.Id, (serviceProvider, key) =>
            {
                ILogger<BlobStorageDataBaseConnector> logger = serviceProvider.GetRequiredService<ILogger<BlobStorageDataBaseConnector>>();
                IAzureClientFactory<BlobServiceClient> factory = serviceProvider.GetRequiredService<IAzureClientFactory<BlobServiceClient>>();
                return new BlobStorageDataBaseConnector(db, containerName, factory, logger);
            });
        }
    }
}