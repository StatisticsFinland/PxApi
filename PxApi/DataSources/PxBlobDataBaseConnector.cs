using Azure;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Azure;
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
    public sealed class PxBlobDataBaseConnector(DataBaseRef dataBase, string containerName, IAzureClientFactory<BlobServiceClient> blobServiceClientFactory, ILogger<PxBlobDataBaseConnector> logger)
        : BlobDataBaseConnector(dataBase, containerName, blobServiceClientFactory)
    {
        /// <inheritdoc/>
        protected override ILogger Logger => logger;

        /// <inheritdoc/>
        protected override bool UseShortFormNames => false;

        /// <inheritdoc/>
        /// <remarks>The version is the ETag of the PX file.</remarks>
        public override async Task<string> GetSourceVersionAsync(PxFileRef file, CancellationToken ct = default)
        {
            using (Logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.CONTROLLER] = nameof(PxBlobDataBaseConnector),
                    [LoggerConsts.FUNCTION] = nameof(GetSourceVersionAsync),
                    [LoggerConsts.PX_FILE] = file.Id,
                    [LoggerConsts.CONTAINER_NAME] = ContainerName
                }))
            {
                Logger.LogDebug("Getting source version for PX file {file_id} from blob storage", file.Id);

                BlobContainerClient containerClient = GetContainerClient();
                string normalizedPath = GetBlobName(file.Id, DataBase, PxBlobPrefix, file.GetHierarchyLevels());
                BlobClient blobClient = containerClient.GetBlobClient(normalizedPath);

                BlobProperties properties;
                try
                {
                    properties = await blobClient.GetPropertiesAsync(cancellationToken: ct);
                }
                catch (RequestFailedException ex) when (ex.Status == 404)
                {
                    throw new FileNotFoundException($"File {file.Id} not found in database {DataBase.Id}, blob storage container {ContainerName}, path {normalizedPath}.", ex);
                }
                return properties.ETag.ToString();
            }
        }
    }
}