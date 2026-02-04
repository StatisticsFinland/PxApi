using Azure.Storage.Blobs;
using Microsoft.Extensions.Azure;
using PxApi.Models;
using PxApi.Utilities;

namespace PxApi.DataSources
{
    /// <summary>
    /// Base class for database connectors backed by Azure Blob Storage.
    /// </summary>
    /// <remarks>
    /// Provides shared blob access functionality for blob-based connectors, including:
    /// <list type="bullet">
    /// <item><description>Resolving a <see cref="BlobContainerClient"/> for the configured container.</description></item>
    /// <item><description>Reading auxiliary (non-PX) files from the <c>px</c> prefix.</description></item>
    /// </list>
    /// Client creation is delegated to an <see cref="IAzureClientFactory{TClient}"/> so that configuration (credentials, endpoints)
    /// can be managed via dependency injection.
    /// </remarks>
    /// <param name="dataBase">Database reference used as the Azure client name when creating blob clients.</param>
    /// <param name="containerName">Name of the Azure blob storage container hosting the database content.</param>
    /// <param name="blobServiceClientFactory">Factory for creating <see cref="BlobServiceClient"/> instances.</param>
    public abstract class BlobDataBaseConnector(DataBaseRef dataBase, string containerName, IAzureClientFactory<BlobServiceClient> blobServiceClientFactory) : DataBaseConnector(dataBase)
    {
        /// <summary>
        /// Name of the configured blob container.
        /// </summary>
        protected string ContainerName => containerName;

        private const string PxBlobPrefix = "px";

        /// <summary>
        /// Attempts to read an auxiliary file from blob storage.
        /// </summary>
        /// <param name="relativePath">Path to the auxiliary file relative to the <see cref="PxBlobPrefix"/> prefix.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>An open, readable stream for the requested auxiliary file.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the auxiliary file does not exist in the configured container.</exception>
        public override async Task<Stream> TryReadAuxiliaryFileAsync(string relativePath, CancellationToken ct = default)
        {
            using (Logger.BeginScope(new Dictionary<string, object>
            {
                [LoggerConsts.DB_ID] = DataBase.Id,
                [LoggerConsts.FUNCTION] = nameof(TryReadAuxiliaryFileAsync),
                [LoggerConsts.AUXILIARY_PATH] = relativePath,
                [LoggerConsts.CONTAINER_NAME] = ContainerName
            }))
            {
                BlobContainerClient containerClient = GetContainerClient();
                string blobName = Path.Combine(PxBlobPrefix, relativePath);
                BlobClient blob = containerClient.GetBlobClient(blobName);
                if (!await blob.ExistsAsync(ct))
                {
                    Logger.LogWarning("Aux file {AuxFile} not found", blobName);
                    throw new FileNotFoundException("Auxiliary file not found", blobName);
                }
                return await blob.OpenReadAsync(cancellationToken: ct);
            }
        }

        /// <summary>
        /// Opens a read-only stream for the specified PX file in blob storage.
        /// </summary>
        /// <param name="file">PX file to open.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>An open, readable stream positioned at the beginning of the PX file content.</returns>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="file"/> does not belong to this connector's database.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the referenced blob does not exist in the configured container.</exception>
        protected override async Task<Stream> OpenPxFileStreamAsync(PxFileRef file, CancellationToken ct = default)
        {
            using (Logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
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

                if (!await blobClient.ExistsAsync(ct))
                {
                    Logger.LogError("PX file {FileId} not found in blob storage path {Path}", file.Id, file.FilePath);
                    throw new FileNotFoundException($"File {file.Id} not found in blob storage path {file.FilePath}.");
                }
                return await blobClient.OpenReadAsync(cancellationToken: ct);
            }
        }

        /// <summary>
        /// Creates a blob container client for the configured database and container.
        /// </summary>
        /// <remarks>
        /// The underlying <see cref="BlobServiceClient"/> is created using the database id as the Azure client name,
        /// allowing multiple different storage accounts/endpoints to be configured via dependency injection.
        /// </remarks>
        /// <returns>A container client for <see cref="ContainerName"/>.</returns>
        protected BlobContainerClient GetContainerClient()
        {
            BlobServiceClient serviceClient = blobServiceClientFactory.CreateClient(DataBase.Id);
            return serviceClient.GetBlobContainerClient(containerName);
        }
    }
}
