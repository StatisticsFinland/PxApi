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
    public class FileShareDataBaseConnector(DataBaseRef dataBase, string shareName, IAzureClientFactory<ShareServiceClient> shareServiceClientFactory, ILogger<FileShareDataBaseConnector> logger) : IDataBaseConnector
    {
        private readonly DataBaseRef _dataBase = dataBase;
        private readonly ILogger<FileShareDataBaseConnector> _logger = logger;
        private readonly IAzureClientFactory<ShareServiceClient> _shareServiceClientFactory = shareServiceClientFactory;

        /// <inheritdoc/>
        public DataBaseRef DataBase => _dataBase;

        /// <inheritdoc/>
        public async Task<string[]> GetAllFilesAsync()
        {
            using (_logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.CONTROLLER] = nameof(FileShareDataBaseConnector),
                    [LoggerConsts.FUNCTION] = nameof(GetAllFilesAsync)
                }))
            {
                _logger.LogDebug("Getting all files from file share {ShareName}", shareName);

                List<string> fileNames = [];

                ShareDirectoryClient rootDirectory = CreateShareClient().GetRootDirectoryClient();

                await ListAllFilesRecursivelyAsync(rootDirectory, string.Empty, fileNames);

                _logger.LogDebug("Found {Count} PX files in file share {ShareName}", fileNames.Count, shareName);
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
                    [LoggerConsts.CONTROLLER] = nameof(FileShareDataBaseConnector),
                    [LoggerConsts.FUNCTION] = nameof(ReadPxFileAsync),
                    [LoggerConsts.PX_FILE] = file.Id
                }))
            {
                if (file.DataBase.Id != DataBase.Id)
                {
                    _logger.LogWarning("The file does not belong to the database.");
                    throw new InvalidOperationException("The file does not belong to the database.");
                }

                _logger.LogDebug("Reading PX file {FileId} from file share", file.Id);
                ShareDirectoryClient directoryClient = CreateShareClient().GetRootDirectoryClient();
                ShareFileClient? fileClient = await FindPxFileAsync(directoryClient, file.Id);
                if (fileClient == null || !await fileClient.ExistsAsync())
                {
                    _logger.LogError("PX file {FileId} not found in file share", file.Id);
                    throw new FileNotFoundException($"File {file.Id} not found in file share {shareName}");
                }
                return await fileClient.OpenReadAsync();
            }
        }

        /// <inheritdoc/>
        public async Task<DateTime> GetLastWriteTimeAsync(PxFileRef file)
        {
            using (_logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.CONTROLLER] = nameof(FileShareDataBaseConnector),
                    [LoggerConsts.FUNCTION] = nameof(GetLastWriteTimeAsync),
                    [LoggerConsts.PX_FILE] = file.Id
                }))
            {
                _logger.LogDebug("Getting last write time for PX file {FileId} from file share", file.Id);

                ShareDirectoryClient directoryClient = CreateShareClient().GetRootDirectoryClient();
                ShareFileClient? fileClient = await FindPxFileAsync(directoryClient, file.Id);
                if (fileClient == null || !await fileClient.ExistsAsync())
                {
                    _logger.LogError("PX file {FileId} not found in file share", file.Id);
                    throw new FileNotFoundException($"File {file.Id} not found in file share {shareName}");
                }

                ShareFileProperties properties = await fileClient.GetPropertiesAsync();
                return properties.LastModified.DateTime;
            }
        }

        /// <inheritdoc/>
        public async Task<Stream> TryReadAuxiliaryFileAsync(string relativePath)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                [LoggerConsts.DB_ID] = DataBase.Id,
                [LoggerConsts.CONTROLLER] = nameof(FileShareDataBaseConnector),
                [LoggerConsts.FUNCTION] = nameof(TryReadAuxiliaryFileAsync),
                [LoggerConsts.AUXILIARY_PATH] = relativePath
            }))
            {
                string normalized = relativePath.Replace('\\', '/');
                ShareDirectoryClient root = CreateShareClient().GetRootDirectoryClient();
                if (string.IsNullOrEmpty(normalized))
                {
                    _logger.LogWarning("Auxiliary path empty");
                    throw new FileNotFoundException("Auxiliary path empty", normalized);
                }
                string[] parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
                ShareDirectoryClient currentDir = root;
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    currentDir = currentDir.GetSubdirectoryClient(parts[i]);
                }
                ShareFileClient fileClient = currentDir.GetFileClient(parts[^1]);
                if (!await fileClient.ExistsAsync())
                {
                    _logger.LogWarning("Aux file {AuxFile} not found", normalized);
                    throw new FileNotFoundException("Auxiliary file not found", normalized);
                }
                MemoryStream ms = new();
                ShareFileDownloadInfo dl = (await fileClient.DownloadAsync()).Value;
                await dl.Content.CopyToAsync(ms);
                ms.Position = 0;
                return ms;
            }
        }

        private static async Task<ShareFileClient?> FindPxFileAsync(ShareDirectoryClient directory, string fileId)
        {
            await foreach (ShareFileItem item in directory.GetFilesAndDirectoriesAsync())
            {
                if (item.IsDirectory)
                {
                    ShareDirectoryClient subDir = directory.GetSubdirectoryClient(item.Name);
                    ShareFileClient? found = await FindPxFileAsync(subDir, fileId);
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

        private static async Task ListAllFilesRecursivelyAsync(ShareDirectoryClient directory, string path, List<string> fileNames)
        {
                // List all files in current directory
                await foreach (ShareFileItem item in directory.GetFilesAndDirectoriesAsync())
                {
                if (item.IsDirectory)
                {
                    // Recursively traverse subdirectories
                    string subDirPath = string.IsNullOrEmpty(path) ? item.Name : $"{path}/{item.Name}";
                    ShareDirectoryClient subDir = directory.GetSubdirectoryClient(item.Name);
                    await ListAllFilesRecursivelyAsync(subDir, subDirPath, fileNames);
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
            ShareServiceClient serviceClient = _shareServiceClientFactory.CreateClient(_dataBase.Id);
            return serviceClient.GetShareClient(shareName);
        }
    }
}