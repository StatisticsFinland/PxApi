using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using Azure;
using PxApi.ModelBuilders;
using PxApi.Models;
using PxApi.Utilities;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Azure;

namespace PxApi.DataSources
{
    /// <summary>
    /// Data source for using database in Azure Blob Storage.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class BlobStorageDataBaseConnector : IDataBaseConnector
    {
        private readonly DataBaseRef _dataBase; 
        private readonly string _containerName;
        private readonly ILogger<BlobStorageDataBaseConnector> _logger;
        private readonly IAzureClientFactory<BlobServiceClient> _blobServiceClientFactory;

        /// <inheritdoc/>
        public DataBaseRef DataBase => _dataBase;

        /// <summary>
        /// Initializes a new instance of the <see cref="BlobStorageDataBaseConnector"/> class.
        /// </summary>
        /// <param name="dataBase">The database ID.</param>
        /// <param name="containerName">Blob container name.</param>
        /// <param name="blobServiceClientFactory">Azure client factory for <see cref="BlobServiceClient"/>.</param>
        /// <param name="logger">Logger for the connector.</param>
        public BlobStorageDataBaseConnector(DataBaseRef dataBase, string containerName, IAzureClientFactory<BlobServiceClient> blobServiceClientFactory, ILogger<BlobStorageDataBaseConnector> logger)
        {
            _dataBase = dataBase;
            _containerName = containerName;
            _logger = logger;
            _blobServiceClientFactory = blobServiceClientFactory;
        }

        /// <inheritdoc/>
        public async Task<string[]> GetAllFilesAsync()
        {
            using (_logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.CONTROLLER] = nameof(BlobStorageDataBaseConnector),
                    [LoggerConsts.FUNCTION] = nameof(GetAllFilesAsync)
                }))
            {
                _logger.LogDebug("Getting all files from blob storage container {ContainerName}", _containerName);

                List<string> fileNames = [];

                BlobContainerClient containerClient = GetContainerClient();
                await containerClient.CreateIfNotExistsAsync();

                AsyncPageable<BlobItem> blobs = containerClient.GetBlobsAsync();

                await foreach (BlobItem blob in blobs)
                {
                    if (blob.Name.EndsWith(PxFileConstants.FILE_ENDING, StringComparison.OrdinalIgnoreCase))
                    {
                        fileNames.Add(blob.Name);
                    }
                }

                _logger.LogDebug("Found {Count} PX files in container {ContainerName}", fileNames.Count, _containerName);
                return [.. fileNames];
            }
        }

        /// <inheritdoc/>
        public async Task<Stream> ReadPxFileAsync(PxFileRef file)
        {
            using (_logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.CONTROLLER] = nameof(BlobStorageDataBaseConnector),
                    [LoggerConsts.FUNCTION] = nameof(ReadPxFileAsync),
                    [LoggerConsts.PX_FILE] = file.Id
                }))
            {
                _logger.LogDebug("Reading PX file {FileId} from blob storage", file.Id);

                if (file.DataBase.Id != DataBase.Id)
                {
                    _logger.LogWarning("The file does not belong to the database.");
                    throw new InvalidOperationException("The file does not belong to the database.");
                }

                BlobContainerClient containerClient = GetContainerClient();
                BlobClient blobClient = containerClient.GetBlobClient(file.Id);

                if (!await blobClient.ExistsAsync())
                {
                    _logger.LogError("PX file {FileId} not found in blob storage", file.Id);
                    throw new FileNotFoundException($"File {file.Id} not found in blob storage container {_containerName}");
                }
                return await blobClient.OpenReadAsync();
            }
        }

        /// <inheritdoc/>
        public async Task<DateTime> GetLastWriteTimeAsync(PxFileRef file)
        {
            using (_logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.CONTROLLER] = nameof(BlobStorageDataBaseConnector),
                    [LoggerConsts.FUNCTION] = nameof(GetLastWriteTimeAsync),
                    [LoggerConsts.PX_FILE] = file.Id
                }))
            {
                _logger.LogDebug("Getting last write time for PX file {FileId} from blob storage", file.Id);

                BlobContainerClient containerClient = GetContainerClient();
                BlobClient blobClient = containerClient.GetBlobClient(file.Id);

                if (!await blobClient.ExistsAsync())
                {
                    _logger.LogError("PX file {FileId} not found in blob storage", file.Id);
                    throw new FileNotFoundException($"File {file.Id} not found in blob storage container {_containerName}");
                }

                BlobProperties properties = await blobClient.GetPropertiesAsync();
                return properties.LastModified.DateTime;
            }
        }

        /// <inheritdoc/>
        public async Task<Stream> TryReadAuxiliaryFileAsync(string relativePath)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                [LoggerConsts.DB_ID] = DataBase.Id,
                [LoggerConsts.CONTROLLER] = nameof(BlobStorageDataBaseConnector),
                [LoggerConsts.FUNCTION] = nameof(TryReadAuxiliaryFileAsync),
                [LoggerConsts.AUXILIARY_PATH] = relativePath
            }))
            {
                BlobContainerClient containerClient = GetContainerClient();
                await containerClient.CreateIfNotExistsAsync();
                string blobName = relativePath.Replace('\\', '/');
                BlobClient blob = containerClient.GetBlobClient(blobName);
                if (!await blob.ExistsAsync())
                {
                    _logger.LogWarning("Aux file {AuxFile} not found", blobName);
                    throw new FileNotFoundException("Auxiliary file not found", blobName);
                }
                MemoryStream ms = new();
                await blob.DownloadToAsync(ms);
                ms.Position = 0;
                return ms;
            }
        }

        private BlobContainerClient GetContainerClient()
        {
            BlobServiceClient serviceClient = _blobServiceClientFactory.CreateClient(_dataBase.Id);
            return serviceClient.GetBlobContainerClient(_containerName);
        }
    }
}