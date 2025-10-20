using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using PxApi.ModelBuilders;
using PxApi.Models;
using PxApi.Utilities;
using System.Diagnostics.CodeAnalysis;

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
        private readonly BlobContainerClient _containerClient;

        /// <inheritdoc/>
        public DataBaseRef DataBase => _dataBase;

        /// <summary>
        /// Initializes a new instance of the <see cref="BlobStorageDataBaseConnector"/> class.
        /// </summary>
        /// <param name="dataBase">The database ID.</param>
        /// <param name="connectionString">Azure Storage connection string.</param>
        /// <param name="containerName">Blob container name.</param>
        /// <param name="logger">Logger for the connector.</param>
        public BlobStorageDataBaseConnector(DataBaseRef dataBase, string connectionString, string containerName, ILogger<BlobStorageDataBaseConnector> logger)
        {
            _dataBase = dataBase;
            _containerName = containerName;
            _logger = logger;
            _containerClient = new BlobContainerClient(connectionString, _containerName);
        }

        /// <inheritdoc/>
        public async Task<string[]> GetAllFilesAsync()
        {
            using (_logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.CLASS_NAME] = nameof(BlobStorageDataBaseConnector),
                    [LoggerConsts.METHOD_NAME] = nameof(GetAllFilesAsync)
                }))
            {
                _logger.LogDebug("Getting all files from blob storage container {ContainerName}", _containerName);

                List<string> fileNames = [];

                await _containerClient.CreateIfNotExistsAsync();

                AsyncPageable<BlobItem> blobs = _containerClient.GetBlobsAsync();

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
        public Stream ReadPxFile(PxFileRef file)
        {
            using (_logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.CLASS_NAME] = nameof(BlobStorageDataBaseConnector),
                    [LoggerConsts.METHOD_NAME] = nameof(ReadPxFile),
                    [LoggerConsts.PX_FILE] = file.Id
                }))
            {
                _logger.LogDebug("Reading PX file {FileId} from blob storage", file.Id);

                if (file.DataBase.Id != DataBase.Id)
                {
                    _logger.LogWarning("The file does not belong to the database.");
                    throw new InvalidOperationException("The file does not belong to the database.");
                }

                BlobClient blobClient = _containerClient.GetBlobClient(file.Id);

                if (!blobClient.Exists())
                {
                    _logger.LogError("PX file {FileId} not found in blob storage", file.Id);
                    throw new FileNotFoundException($"File {file.Id} not found in blob storage container {_containerName}");
                }

                MemoryStream memoryStream = new();
                blobClient.DownloadTo(memoryStream);
                memoryStream.Position = 0;

                return memoryStream;
            }
        }

        /// <inheritdoc/>
        public async Task<DateTime> GetLastWriteTimeAsync(PxFileRef file)
        {
            using (_logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.CLASS_NAME] = nameof(BlobStorageDataBaseConnector),
                    [LoggerConsts.METHOD_NAME] = nameof(GetLastWriteTimeAsync),
                    [LoggerConsts.PX_FILE] = file.Id
                }))
            {
                _logger.LogDebug("Getting last write time for PX file {FileId} from blob storage", file.Id);

                BlobClient blobClient = _containerClient.GetBlobClient(file.Id);

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
                [LoggerConsts.CLASS_NAME] = nameof(BlobStorageDataBaseConnector),
                [LoggerConsts.METHOD_NAME] = nameof(TryReadAuxiliaryFileAsync),
                [LoggerConsts.AUXILIARY_PATH] = relativePath
            }))
            {
                await _containerClient.CreateIfNotExistsAsync();
                string blobName = relativePath.Replace('\\', '/');
                BlobClient blob = _containerClient.GetBlobClient(blobName);
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
    }
}