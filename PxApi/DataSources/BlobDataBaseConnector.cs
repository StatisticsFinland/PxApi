using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Azure;
using PxApi.ModelBuilders;
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

        /// <summary>
        /// Root level for all px and auxiliary files in blob storage.
        /// </summary>
        protected const string PxBlobPrefix = "px";

        /// <summary>
        /// Indicates whether the px files are identified by short form names e.g. 12ts or the old long format statfin_tyonv_pxt_12ts.
        /// </summary>
        protected abstract bool UseShortFormNames { get; }

        /// <inheritdoc/>
        public override async Task<PxFileRef[]> GetAllFilesAsync(CancellationToken ct = default)
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
                List<PxFileRef> files = [];
                string blobPrefix = $"{PxBlobPrefix}/{DataBase.Id}/";

                BlobContainerClient containerClient = GetContainerClient();
                AsyncPageable<BlobItem> blobs = containerClient.GetBlobsAsync(cancellationToken: ct);

                await foreach (BlobItem blob in blobs)
                {
                    if (blob.Name.EndsWith(PxFileConstants.FILE_ENDING, StringComparison.OrdinalIgnoreCase))
                    {
                        string relativePath = blob.Name.StartsWith(blobPrefix, StringComparison.OrdinalIgnoreCase)
                            ? blob.Name[blobPrefix.Length..]
                            : blob.Name;

                        string[] segments = relativePath.Split('/');
                        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(segments[^1]);
                        string tableId = UseShortFormNames
                            ? fileNameWithoutExtension.Split('_')[^1]
                            : fileNameWithoutExtension;
                        string[]? hierarchy = segments.Length > 1 ? segments[..^1] : null;

                        files.Add(PxFileRef.ValidateAndCreate(tableId, DataBase, hierarchy));
                    }
                }

                Logger.LogDebug("Found {Count} PX files.", files.Count);
                return [.. files];
            }
        }

        /// <summary>
        /// Attempts to read an auxiliary file from blob storage.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="hierarchy"></param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>An open, readable stream for the requested auxiliary file.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the auxiliary file does not exist in the configured container.</exception>
        public override async Task<Stream> TryReadAuxiliaryFileAsync(string fileName, string[]? hierarchy, CancellationToken ct = default)
        {
            using (Logger.BeginScope(new Dictionary<string, object>
            {
                [LoggerConsts.DB_ID] = DataBase.Id,
                [LoggerConsts.FUNCTION] = nameof(TryReadAuxiliaryFileAsync),
                [LoggerConsts.AUXILIARY_PATH] = fileName,
                [LoggerConsts.CONTAINER_NAME] = ContainerName
            }))
            {
                BlobContainerClient containerClient = GetContainerClient();
                string blobName = hierarchy != null ? string.Join('/', hierarchy) + "/" + fileName : fileName;
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
                string blobPath = GetBlobName(file.Id, file.DataBase, PxBlobPrefix, file.GetHierarchyLevels());
                BlobClient blobClient = containerClient.GetBlobClient(blobPath);

                if (!await blobClient.ExistsAsync(ct))
                {
                    Logger.LogError("PX file {FileId} not found in blob storage path {Path}", file.Id, blobPath);
                    throw new FileNotFoundException($"File {file.Id} not found in blob storage path {blobPath}.");
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

        /// <summary>
        /// Constructs the full blob name for a file based on its name and optional hierarchy.
        /// </summary>
        /// <param name="fileName">The name of the file.</param>
        /// <param name="db"></param>
        /// <param name="root"></param>
        /// <param name="hierarchy">Optional hierarchy of folders leading to the file.</param>
        /// <returns>The full blob name.</returns>
        protected static string GetBlobName(string fileName, DataBaseRef db, string root, string[]? hierarchy)
        {
            List<string> completePath = [root, db.Id];
            if (hierarchy != null && hierarchy.Length > 0) completePath.AddRange(hierarchy);
            completePath.Add(fileName);
            return string.Join('/', completePath);
        }
    }
}
