using Azure.Storage.Blobs;
using Microsoft.Extensions.Azure;
using PxApi.Models;
using PxApi.Utilities;

namespace PxApi.DataSources
{
    public abstract class BlobDataBaseConnector(DataBaseRef dataBase, string containerName, IAzureClientFactory<BlobServiceClient> blobServiceClientFactory) : DataBaseConnector(dataBase)
    {
        protected string ContainerName => containerName;

        /// <inheritdoc/>
        public override async Task<Stream> TryReadAuxiliaryFileAsync(string relativePath, CancellationToken ct)
        {
            using (Logger.BeginScope(new Dictionary<string, object>
            {
                [LoggerConsts.DB_ID] = DataBase.Id,
                [LoggerConsts.CONTROLLER] = nameof(PxBlobDataBaseConnector),
                [LoggerConsts.FUNCTION] = nameof(TryReadAuxiliaryFileAsync),
                [LoggerConsts.AUXILIARY_PATH] = relativePath
            }))
            {
                BlobContainerClient containerClient = GetContainerClient();
                // TODO: This needs to take into account the database name and px prefix
                string blobName = relativePath.Replace('\\', '/');
                BlobClient blob = containerClient.GetBlobClient(blobName);
                if (!await blob.ExistsAsync(ct))
                {
                    Logger.LogWarning("Aux file {AuxFile} not found", blobName);
                    throw new FileNotFoundException("Auxiliary file not found", blobName);
                }
                return await blob.OpenReadAsync(cancellationToken: ct);
            }
        }

        protected BlobContainerClient GetContainerClient()
        {
            BlobServiceClient serviceClient = blobServiceClientFactory.CreateClient(DataBase.Id);
            return serviceClient.GetBlobContainerClient(containerName);
        }
    }
}
