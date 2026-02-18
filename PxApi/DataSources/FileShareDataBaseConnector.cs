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
        public override async Task<string[]> GetAllFilesAsync(CancellationToken ct = default)
        {
            using (Logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.FUNCTION] = nameof(GetAllFilesAsync)
                }))
            {
                Logger.LogDebug("Getting all files from file share {ShareName}", shareName);
                List<string> fileNames = [];

                ShareDirectoryClient rootDirectory = CreateShareClient().GetRootDirectoryClient();

                await ListAllFilesRecursivelyAsync(rootDirectory, string.Empty, fileNames, ct);

                Logger.LogDebug("Found {Count} PX files in file share {ShareName}", fileNames.Count, shareName);
                return [.. fileNames];
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
                ShareDirectoryClient directoryClient = CreateShareClient().GetRootDirectoryClient();
                ShareFileClient? fileClient = await FindPxFileAsync(directoryClient, file.Id, ct);
                if (fileClient == null || !await fileClient.ExistsAsync(ct))
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

                ShareDirectoryClient directoryClient = CreateShareClient().GetRootDirectoryClient();
                ShareFileClient? fileClient = await FindPxFileAsync(directoryClient, file.Id, ct);
                if (fileClient == null || !await fileClient.ExistsAsync(ct))
                {
                    Logger.LogError("PX file {FileId} not found in file share", file.Id);
                    throw new FileNotFoundException($"File {file.Id} not found in file share {shareName}");
                }

                ShareFileProperties properties = await fileClient.GetPropertiesAsync(cancellationToken: ct);
                return properties.LastModified.DateTime;
            }
        }

        /// <inheritdoc/>
        public override async Task<Stream> TryReadAuxiliaryFileAsync(string relativePath, CancellationToken ct = default)
        {
            using (Logger.BeginScope(new Dictionary<string, object>
            {
                [LoggerConsts.DB_ID] = DataBase.Id,
                [LoggerConsts.FUNCTION] = nameof(TryReadAuxiliaryFileAsync),
                [LoggerConsts.AUXILIARY_PATH] = relativePath
            }))
            {
                string normalized = relativePath.Replace('\\', '/');
                ShareDirectoryClient root = CreateShareClient().GetRootDirectoryClient();
                if (string.IsNullOrEmpty(normalized))
                {
                    Logger.LogWarning("Auxiliary path empty");
                    throw new FileNotFoundException("Auxiliary path empty", normalized);
                }
                string[] parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
                ShareDirectoryClient currentDir = root;
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    currentDir = currentDir.GetSubdirectoryClient(parts[i]);
                }
                ShareFileClient fileClient = currentDir.GetFileClient(parts[^1]);
                if (!await fileClient.ExistsAsync(ct))
                {
                    Logger.LogWarning("Aux file {AuxFile} not found", normalized);
                    throw new FileNotFoundException("Auxiliary file not found", normalized);
                }

                return await fileClient.OpenReadAsync(cancellationToken: ct);
            }
        }

        private static async Task<ShareFileClient?> FindPxFileAsync(ShareDirectoryClient directory, string fileId, CancellationToken ct = default)
        {
            await foreach (ShareFileItem item in directory.GetFilesAndDirectoriesAsync(cancellationToken: ct))
            {
                ct.ThrowIfCancellationRequested();
                if (item.IsDirectory)
                {
                    ShareDirectoryClient subDir = directory.GetSubdirectoryClient(item.Name);
                    ShareFileClient? found = await FindPxFileAsync(subDir, fileId, ct);
                    if (found != null)
                    {
                        return found;
                    }
                }
                else if (item.Name.Equals(fileId + PxFileConstants.FILE_ENDING, StringComparison.OrdinalIgnoreCase))
                {
                    return directory.GetFileClient(item.Name);
                }
            }
            return null;
        }

        private static async Task ListAllFilesRecursivelyAsync(ShareDirectoryClient directory, string path, List<string> fileNames, CancellationToken ct)
        {
            // List all files in current directory
            await foreach (ShareFileItem item in directory.GetFilesAndDirectoriesAsync(cancellationToken: ct))
            {
                if (item.IsDirectory)
                {
                    // Recursively traverse subdirectories
                    string subDirPath = string.IsNullOrEmpty(path) ? item.Name : $"{path}/{item.Name}";
                    ShareDirectoryClient subDir = directory.GetSubdirectoryClient(item.Name);
                    await ListAllFilesRecursivelyAsync(subDir, subDirPath, fileNames, ct);
                }
                else
                {
                    if (item.Name.EndsWith(PxFileConstants.FILE_ENDING, StringComparison.OrdinalIgnoreCase))
                    {
                        fileNames.Add(item.Name);
                    }
                }
            }
        }

        private ShareClient CreateShareClient()
        {
            ShareServiceClient serviceClient = shareServiceClientFactory.CreateClient(DataBase.Id);
            return serviceClient.GetShareClient(shareName);
        }
    }
}