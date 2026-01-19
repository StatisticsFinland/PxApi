using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using Azure;
using Microsoft.Extensions.Azure;
using PxApi.ModelBuilders;
using PxApi.Models;
using PxApi.Utilities;
using System.Diagnostics.CodeAnalysis;

namespace PxApi.DataSources
{
    /// <summary>
    /// Data source for using database in Azure Blob Storage.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="PxBlobDataBaseConnector"/> class.
    /// </remarks>
    /// <param name="dataBase">The database ID.</param>
    /// <param name="containerName">Blob container name.</param>
    /// <param name="blobServiceClientFactory">Azure client factory for <see cref="BlobServiceClient"/>.</param>
    /// <param name="logger">Logger for the connector.</param>
    [ExcludeFromCodeCoverage]
    public sealed class PxBlobDataBaseConnector(DataBaseRef dataBase, string containerName, IAzureClientFactory<BlobServiceClient> blobServiceClientFactory, ILogger<PxBlobDataBaseConnector> logger) : BlobDataBaseConnector(dataBase, containerName, blobServiceClientFactory)
    {
        protected override ILogger Logger => logger;

        /// <inheritdoc/>
        public override async Task<string[]> GetAllFilesAsync()
        {
            using (Logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.CONTROLLER] = nameof(PxBlobDataBaseConnector),
                    [LoggerConsts.FUNCTION] = nameof(GetAllFilesAsync),
                    [LoggerConsts.CONTAINER_NAME] = ContainerName
                }))
            {
                Logger.LogDebug("Getting all files from blob storage container.");
                List<string> fileNames = [];

                BlobContainerClient containerClient = GetContainerClient();
                AsyncPageable<BlobItem> blobs = containerClient.GetBlobsAsync();

                await foreach (BlobItem blob in blobs)
                {
                    if (blob.Name.EndsWith(PxFileConstants.FILE_ENDING, StringComparison.OrdinalIgnoreCase))
                    {
                        fileNames.Add(blob.Name);
                    }
                }

                Logger.LogDebug("Found {Count} PX files.", fileNames.Count);
                return [.. fileNames];
            }
        }

        protected override async Task<Stream> OpenPxFileStreamAsync(PxFileRef file)
        {
            using (Logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.CONTROLLER] = nameof(PxBlobDataBaseConnector),
                    [LoggerConsts.FUNCTION] = nameof(OpenPxFileStreamAsync),
                    [LoggerConsts.PX_FILE] = file.Id,
                    [LoggerConsts.CONTAINER_NAME] = ContainerName
                }))
            {
                Logger.LogDebug("Reading PX file {FileId} from blob storage", file.Id);

                if (file.DataBase.Id != DataBase.Id)
                {
                    Logger.LogWarning("The file does not belong to the database.");
                    throw new InvalidOperationException("The file does not belong to the database.");
                }

                BlobContainerClient containerClient = GetContainerClient();
                BlobClient blobClient = containerClient.GetBlobClient(file.FilePath);

                if (!await blobClient.ExistsAsync())
                {
                    Logger.LogError("PX file {FileId} not found in blob storage", file.Id);
                    throw new FileNotFoundException($"File {file.Id} not found in blob storage container.");
                }
                return await blobClient.OpenReadAsync();
            }
        }

        /// <inheritdoc/>
        public override async Task<DateTime> GetLastWriteTimeAsync(PxFileRef file)
        {
            using (Logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.CONTROLLER] = nameof(PxBlobDataBaseConnector),
                    [LoggerConsts.FUNCTION] = nameof(GetLastWriteTimeAsync),
                    [LoggerConsts.PX_FILE] = file.Id,
                    [LoggerConsts.CONTAINER_NAME] = ContainerName
                }))
            {
                Logger.LogDebug("Getting last write time for PX file {FileId} from blob storage", file.Id);

                BlobContainerClient containerClient = GetContainerClient();
                BlobClient blobClient = containerClient.GetBlobClient(file.FilePath);

                if (!await blobClient.ExistsAsync())
                {
                    Logger.LogError("PX file {FileId} not found in blob storage", file.Id);
                    throw new FileNotFoundException($"File {file.Id} not found in blob storage container.");
                }

                BlobProperties properties = await blobClient.GetPropertiesAsync();
                return properties.LastModified.DateTime;
            }
        }

    }
}