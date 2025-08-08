using Azure;
using Azure.Identity;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using Px.Utils.PxFile;
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
    /// <param name="sharePath">File share path.</param>
    /// <param name="logger">Logger for the connector.</param>
    [ExcludeFromCodeCoverage]
    public class FileShareDataBaseConnector(DataBaseRef dataBase, string sharePath, ILogger<FileShareDataBaseConnector> logger) : IDataBaseConnector
    {
        private readonly DataBaseRef _dataBase = dataBase;
        private readonly string _sharePath = sharePath;
        private readonly ILogger<FileShareDataBaseConnector> _logger = logger;
        private readonly ShareClient _shareClient = new(new(sharePath), new DefaultAzureCredential(), new()
        {
            ShareTokenIntent = ShareTokenIntent.Backup
        });

        /// <inheritdoc/>
        public DataBaseRef DataBase => _dataBase;

        /// <inheritdoc/>
        public async Task<string[]> GetAllFilesAsync()
        {
            using (_logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.CLASS_NAME] = nameof(FileShareDataBaseConnector),
                    [LoggerConsts.METHOD_NAME] = nameof(GetAllFilesAsync)
                }))
            {
                _logger.LogDebug("Getting all files from file share {SharePath}", _sharePath);
                
                List<string> fileNames = [];
                
                try
                {
                    await _shareClient.CreateIfNotExistsAsync();
                    ShareDirectoryClient rootDirectory = _shareClient.GetRootDirectoryClient();
                    
                    await ListAllFilesRecursivelyAsync(rootDirectory, "", fileNames);
                    
                    _logger.LogDebug("Found {Count} PX files in file share {SharePath}", fileNames.Count, _sharePath);
                    return [.. fileNames];
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting files from file share {SharePath}", _sharePath);
                    throw;
                }
            }
        }

        /// <inheritdoc/>
        public Stream ReadPxFile(PxFileRef file)
        {
            using (_logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.CLASS_NAME] = nameof(FileShareDataBaseConnector),
                    [LoggerConsts.METHOD_NAME] = nameof(ReadPxFile),
                    [LoggerConsts.PX_FILE] = file.Id
                }))
            {
                if (file.DataBase.Id != DataBase.Id)
                {
                    _logger.LogWarning("The file does not belong to the database.");
                    throw new InvalidOperationException("The file does not belong to the database.");
                }

                _logger.LogDebug("Reading PX file {FileId} from file share", file.Id);
                
                try
                {
                    ShareDirectoryClient directoryClient = _shareClient.GetRootDirectoryClient();
                    ShareFileClient? fileClient = FindPxFileAsync(directoryClient, file.Id).GetAwaiter().GetResult();
                    if (fileClient == null || !fileClient.Exists())
                    {
                        _logger.LogError("PX file {FileId} not found in file share", file.Id);
                        throw new FileNotFoundException($"File {file.Id} not found in file share {_sharePath}");
                    }
                    
                    MemoryStream memoryStream = new();
                    Response<ShareFileDownloadInfo> dl = fileClient.Download();
                    dl.Value.Content.CopyTo(memoryStream);
                    memoryStream.Position = 0;
                    
                    return memoryStream;
                }
                catch (Exception ex) when (ex is not FileNotFoundException)
                {
                    _logger.LogError(ex, "Error reading PX file {FileId} from file share", file.Id);
                    throw;
                }
            }
        }

        /// <inheritdoc/>
        public async Task<DateTime> GetLastWriteTimeAsync(PxFileRef file)
        {
            using (_logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.CLASS_NAME] = nameof(FileShareDataBaseConnector),
                    [LoggerConsts.METHOD_NAME] = nameof(GetLastWriteTimeAsync),
                    [LoggerConsts.PX_FILE] = file.Id
                }))
            {
                _logger.LogDebug("Getting last write time for PX file {FileId} from file share", file.Id);
                
                try
                {
                    ShareDirectoryClient directoryClient = _shareClient.GetRootDirectoryClient();
                    ShareFileClient? fileClient = await FindPxFileAsync(directoryClient, file.Id);
                    if (fileClient == null || !await fileClient.ExistsAsync())
                    {
                        _logger.LogError("PX file {FileId} not found in file share", file.Id);
                        throw new FileNotFoundException($"File {file.Id} not found in file share {_sharePath}");
                    }
                    
                    ShareFileProperties properties = await fileClient.GetPropertiesAsync();
                    return properties.LastModified.DateTime;
                }
                catch (Exception ex) when (ex is not FileNotFoundException)
                {
                    _logger.LogError(ex, "Error getting last write time for PX file {FileId} from file share", file.Id);
                    throw;
                }
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

        private async Task ListAllFilesRecursivelyAsync(ShareDirectoryClient directory, string path, List<string> fileNames)
        {
            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing files in directory {Path}", path);
                throw;
            }
        }
    }
}