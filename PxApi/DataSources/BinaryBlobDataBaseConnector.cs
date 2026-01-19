using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using Azure;
using Microsoft.Extensions.Azure;
using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Metadata;
using PxApi.Models;
using PxApi.Utilities;
using System.Text.Json;

namespace PxApi.DataSources
{
    public class BinaryBlobDataBaseConnector(DataBaseRef dataBase, string containerName, IAzureClientFactory<BlobServiceClient> blobServiceClientFactory, ILogger<PxBlobDataBaseConnector> logger) : BlobDataBaseConnector(dataBase, containerName, blobServiceClientFactory)
    {
        protected override ILogger Logger => logger;

        private const string MetaPrefix = "meta/";
        private const string MetaFileSuffix = ".meta.json";

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
                Logger.LogDebug("Getting all meta files from blob storage container.");
                List<string> fileNames = [];

                BlobContainerClient containerClient = GetContainerClient();
                AsyncPageable<BlobItem> blobs = containerClient.GetBlobsAsync(prefix: MetaPrefix);

                await foreach (BlobItem blob in blobs)
                {
                    if (blob.Name.EndsWith(MetaFileSuffix, StringComparison.OrdinalIgnoreCase))
                    {
                        fileNames.Add(blob.Name);
                    }
                }

                Logger.LogDebug("Found {Count} meta files.", fileNames.Count);
                return [.. fileNames];
            }
        }

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
                Logger.LogDebug("Getting last write time for meta file {FileId} from blob storage", file.Id);

                BlobContainerClient containerClient = GetContainerClient();
                string blobName = MetaPrefix + file.Id + MetaFileSuffix;
                BlobClient blobClient = containerClient.GetBlobClient(blobName);

                if (!await blobClient.ExistsAsync())
                {
                    Logger.LogError("Meta file for id {FileId} not found in blob storage", file.Id);
                    throw new FileNotFoundException($"Meta file for id {file.Id} not found in blob storage container.");
                }

                BlobProperties properties = await blobClient.GetPropertiesAsync();
                return properties.LastModified.DateTime;
            }
        }

        public override Task<DoubleDataValue[]> ReadDataAsync(PxFileRef file, IMatrixMap targetMap, IReadOnlyMatrixMetadata meta)
        {
            throw new NotImplementedException("BinaryBlobDataBaseConnector does not support reading data.");
        }

        public override async Task<IReadOnlyMatrixMetadata> ReadMetadataAsync(PxFileRef file)
        {
            using (Logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.FUNCTION] = nameof(ReadMetadataAsync),
                    [LoggerConsts.PX_FILE] = file.Id,
                    [LoggerConsts.CONTAINER_NAME] = ContainerName
                }))
            {
                Logger.LogDebug("Reading metadata for meta file {FileId} from blob storage", file.Id);

                BlobContainerClient containerClient = GetContainerClient();
                string blobName = MetaPrefix + file.Id + MetaFileSuffix;
                BlobClient blobClient = containerClient.GetBlobClient(blobName);

                if (!await blobClient.ExistsAsync())
                {
                    Logger.LogError("Meta file for id {FileId} not found in blob storage", file.Id);
                    throw new FileNotFoundException($"Meta file for id {file.Id} not found in blob storage container.");
                }

                using Stream stream = await blobClient.OpenReadAsync();
                JsonSerializerOptions options = new()
                {
                    PropertyNameCaseInsensitive = true
                };
                MatrixMetadata? metadata = await JsonSerializer.DeserializeAsync<MatrixMetadata>(stream, options);
                if (metadata is null)
                {
                    Logger.LogError("Failed to deserialize metadata for id {FileId}", file.Id);
                    throw new InvalidDataException("Failed to deserialize metadata file.");
                }
                return metadata;
            }
        }

    }
}
