using Azure.Storage.Files.Shares.Models;
using Azure.Storage.Files.Shares;
using Microsoft.Extensions.Azure;
using PxApi.ModelBuilders;
using PxApi.Models;
using PxApi.Utilities;
using System.Diagnostics.CodeAnalysis;

namespace PxApi.DataSources
{
    /// <summary>
    /// Data source for using database on a file share.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="FileShareDataBaseConnector"/> class.
    /// </remarks>
    /// <param name="dataBase">The database ID.</param>
    /// <param name="shareName">File share name.</param>
    /// <param name="shareServiceClientFactory">Azure client factory for ShareServiceClient.</param>
    /// <param name="logger">Logger for the connector.</param>
    [ExcludeFromCodeCoverage]
    public class FileShareDataBaseConnector(DataBaseRef dataBase, string shareName, IAzureClientFactory<ShareServiceClient> shareServiceClientFactory, ILogger<FileShareDataBaseConnector> logger) : DataBaseConnector(dataBase)
    {

        /// <inheritdoc/>
        protected override ILogger Logger => logger;

        /// <inheritdoc/>
        public override async Task CheckConnectionAsync(CancellationToken ct = default)
        {
            ShareDirectoryClient directoryClient = GetDatabaseDirectoryClient();
            await directoryClient.ExistsAsync(ct);
        }

        /// <inheritdoc/>
        public override async Task<PxFileRef[]> GetAllFilesAsync(CancellationToken ct = default)
        {
            using (Logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.FUNCTION] = nameof(GetAllFilesAsync)
                }))
            {
                Logger.LogDebug("Getting all files from file share {ShareName}", shareName);
                List<PxFileRef> files = [];

                ShareDirectoryClient dbDirectory = GetDatabaseDirectoryClient();

                await ListAllFilesRecursivelyAsync(dbDirectory, [], files, DataBase, ct);

                Logger.LogDebug("Found {Count} PX files in file share {ShareName}", files.Count, shareName);
                return [.. files];
            }
        }

        /// <inheritdoc/>
        protected override async Task<Stream> OpenPxFileStreamAsync(PxFileRef file, CancellationToken ct = default)
        {
            using (Logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.FUNCTION] = nameof(OpenPxFileStreamAsync),
                    [LoggerConsts.PX_FILE] = file.Id
                }))
            {
                if (file.DataBase.Id != DataBase.Id)
                {
                    Logger.LogWarning("The file does not belong to the database.");
                    throw new InvalidOperationException("The file does not belong to the database.");
                }

                Logger.LogDebug("Reading PX file {FileId} from file share", file.Id);
                ShareFileClient fileClient = GetFileClient(file.Id + PxFileConstants.FILE_ENDING, file.GetHierarchyLevels());

                if (!await fileClient.ExistsAsync(ct))
                {
                    Logger.LogError("PX file {FileId} not found in file share", file.Id);
                    throw new FileNotFoundException($"File {file.Id} not found in file share {shareName}");
                }
                return await fileClient.OpenReadAsync(cancellationToken: ct);
            }
        }

        /// <inheritdoc/>
        public override async Task<DateTime> GetLastWriteTimeAsync(PxFileRef file, CancellationToken ct = default)
        {
            using (Logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.FUNCTION] = nameof(GetLastWriteTimeAsync),
                    [LoggerConsts.PX_FILE] = file.Id
                }))
            {
                Logger.LogDebug("Getting last write time for PX file {FileId} from file share", file.Id);

                ShareFileClient fileClient = GetFileClient(file.Id + PxFileConstants.FILE_ENDING, file.GetHierarchyLevels());

                if (!await fileClient.ExistsAsync(ct))
                {
                    Logger.LogError("PX file {FileId} not found in file share", file.Id);
                    throw new FileNotFoundException($"File {file.Id} not found in file share {shareName}");
                }

                ShareFileProperties properties = await fileClient.GetPropertiesAsync(cancellationToken: ct);
                return properties.LastModified.DateTime;
            }
        }

        /// <inheritdoc/>
        public override async Task<Stream> TryReadAuxiliaryFileAsync(string fileName, string[]? hierarchy, CancellationToken ct = default)
        {
            using (Logger.BeginScope(new Dictionary<string, object>
            {
                [LoggerConsts.DB_ID] = DataBase.Id,
                [LoggerConsts.FUNCTION] = nameof(TryReadAuxiliaryFileAsync),
                [LoggerConsts.AUXILIARY_PATH] = fileName
            }))
            {
                ShareFileClient fileClient = GetFileClient(fileName, hierarchy);

                if (!await fileClient.ExistsAsync(ct))
                {
                    Logger.LogWarning("Aux file {AuxFile} not found", fileName);
                    throw new FileNotFoundException("Auxiliary file not found", fileName);
                }

                return await fileClient.OpenReadAsync(cancellationToken: ct);
            }
        }

        /// <summary>
        /// Gets a <see cref="ShareFileClient"/> for the specified file, navigating through the database directory and optional hierarchy directories.
        /// </summary>
        /// <param name="fileName">The name of the file.</param>
        /// <param name="hierarchy">Optional array of directory names leading to the file, relative to the database directory.</param>
        /// <returns>A <see cref="ShareFileClient"/> for the specified file.</returns>
        private ShareFileClient GetFileClient(string fileName, string[]? hierarchy)
        {
            ShareDirectoryClient directoryClient = GetDatabaseDirectoryClient();

            if (hierarchy is not null)
            {
                foreach (string folder in hierarchy)
                {
                    directoryClient = directoryClient.GetSubdirectoryClient(folder);
                }
            }

            return directoryClient.GetFileClient(fileName);
        }

        private static async Task ListAllFilesRecursivelyAsync(ShareDirectoryClient directory, string[] currentHierarchy, List<PxFileRef> files, DataBaseRef dataBase, CancellationToken ct)
        {
            await foreach (ShareFileItem item in directory.GetFilesAndDirectoriesAsync(cancellationToken: ct))
            {
                if (item.IsDirectory)
                {
                    string[] subHierarchy = [.. currentHierarchy, item.Name];
                    ShareDirectoryClient subDir = directory.GetSubdirectoryClient(item.Name);
                    await ListAllFilesRecursivelyAsync(subDir, subHierarchy, files, dataBase, ct);
                }
                else
                {
                    if (item.Name.EndsWith(PxFileConstants.FILE_ENDING, StringComparison.OrdinalIgnoreCase))
                    {
                        string tableId = Path.GetFileNameWithoutExtension(item.Name);
                        string[]? hierarchy = currentHierarchy.Length > 0 ? currentHierarchy : null;
                        files.Add(PxFileRef.ValidateAndCreate(tableId, dataBase, hierarchy));
                    }
                }
            }
        }

        /// <summary>
        /// Gets a <see cref="ShareDirectoryClient"/> for the database directory within the file share.
        /// </summary>
        /// <returns>A <see cref="ShareDirectoryClient"/> pointing to the database directory.</returns>
        private ShareDirectoryClient GetDatabaseDirectoryClient()
        {
            return CreateShareClient().GetRootDirectoryClient().GetSubdirectoryClient(DataBase.Id);
        }

        private ShareClient CreateShareClient()
        {
            ShareServiceClient serviceClient = shareServiceClientFactory.CreateClient(DataBase.Id);
            return serviceClient.GetShareClient(shareName);
        }
    }
}