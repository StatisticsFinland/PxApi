using PxApi.ModelBuilders;
using PxApi.Models;
using PxApi.Utilities;
using System.Diagnostics.CodeAnalysis;

namespace PxApi.DataSources
{
    /// <summary>
    /// Data source for using database on the local file system.
    /// </summary>
    [ExcludeFromCodeCoverage] // This class is not unit tested because it relies on file system access.
    public class MountedDataBaseConnector(DataBaseRef dataBase, string rootPath, ILogger<MountedDataBaseConnector> logger) : IDataBaseConnector
    {
        /// <inheritdoc/>
        public DataBaseRef DataBase { get; } = dataBase;

        /// <inheritdoc/>
        public Task<string[]> GetAllFilesAsync()
        {
            using (logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.CONTROLLER] = nameof(MountedDataBaseConnector),
                    [LoggerConsts.FUNCTION] = nameof(GetAllFilesAsync)
                }))
            {
                logger.LogDebug("Listing all files");
                string fullPath = Path.GetFullPath(Path.Combine(rootPath, DataBase.Id));
                if (!fullPath.StartsWith(rootPath))
                {
                    logger.LogWarning("Unauthorized access attempt: The database is not in the root path.");
                    throw new UnauthorizedAccessException("The database is not in the root path");
                }

                return Task.Run(() => Directory.GetFiles(
                    fullPath,
                    $"*{PxFileConstants.FILE_ENDING}",
                    SearchOption.AllDirectories));
            }
        }

        /// <inheritdoc/>
        public async Task<Stream> ReadPxFileAsync(PxFileRef file)
        {
            using (logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.CONTROLLER] = nameof(MountedDataBaseConnector),
                    [LoggerConsts.FUNCTION] = nameof(ReadPxFileAsync),
                    [LoggerConsts.PX_FILE] = file.Id
                }))
            {
                logger.LogDebug("Opening file stream");
                if(file.DataBase.Id != DataBase.Id)
                {
                    logger.LogWarning("The file does not belong to the database.");
                    throw new InvalidOperationException("The file does not belong to the database.");
                }

                // Use the FilePath property if it exists and points to a valid file
                if (!string.IsNullOrEmpty(file.FilePath) && File.Exists(file.FilePath))
                {
                    return await Task.FromResult<Stream>(File.OpenRead(file.FilePath));
                }

                // Fall back to constructing the path from components
                string path = Path.Combine(rootPath, file.DataBase.Id, file.Id);
                
                // If the file doesn't exist with just the ID (which is now potentially different from the filename),
                // try to find it by searching for the ID with the file extension
                if (!File.Exists(path))
                {
                    string searchPath = Path.Combine(rootPath, file.DataBase.Id);
                    if (Directory.Exists(searchPath))
                    {
                        string[] matchingFiles = Directory.GetFiles(
                            searchPath, 
                            $"*{file.Id}*{PxFileConstants.FILE_ENDING}", 
                            SearchOption.AllDirectories);
                        
                        if (matchingFiles.Length > 0)
                        {
                            path = matchingFiles[0];
                        }
                    }
                }
                
                return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
        }

        /// <inheritdoc/>
        public async Task<DateTime> GetLastWriteTimeAsync(PxFileRef file)
        {
            using (logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.CONTROLLER] = nameof(MountedDataBaseConnector),
                    [LoggerConsts.FUNCTION] = nameof(GetLastWriteTimeAsync),
                    [LoggerConsts.PX_FILE] = file.Id
                }))
            {
                logger.LogDebug("Getting last write time");
                
                // Use the FilePath property if it exists and points to a valid file
                if (!string.IsNullOrEmpty(file.FilePath) && File.Exists(file.FilePath))
                {
                    return await Task.Run(() => File.GetLastWriteTimeUtc(file.FilePath));
                }
                
                // Fall back to constructing the path from components
                string path = Path.Combine(rootPath, file.DataBase.Id, file.Id);
                return await Task.Run(() => File.GetLastWriteTimeUtc(path));
            }
        }

        /// <inheritdoc/>
        public Task<Stream> TryReadAuxiliaryFileAsync(string relativePath)
        {
            using (logger.BeginScope(new Dictionary<string, object>
            {
                [LoggerConsts.DB_ID] = DataBase.Id,
                [LoggerConsts.CONTROLLER] = nameof(MountedDataBaseConnector),
                [LoggerConsts.FUNCTION] = nameof(TryReadAuxiliaryFileAsync),
                [LoggerConsts.AUXILIARY_PATH] = relativePath
            }))
            {
                string dbRoot = Path.Combine(rootPath, DataBase.Id);
                string fullPath = Path.GetFullPath(Path.Combine(dbRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
                if (!fullPath.StartsWith(dbRoot))
                {
                    logger.LogWarning("Aux file path escaped database root");
                    throw new UnauthorizedAccessException("Auxiliary file path escaped database root.");
                }
                if (!File.Exists(fullPath))
                {
                    logger.LogWarning("Aux file {AuxFile} not found", fullPath);
                    throw new FileNotFoundException("Auxiliary file not found", fullPath);
                }
                Stream s = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                return Task.FromResult(s);
            }
        }
    }
}
