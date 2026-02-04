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
    public class MountedDataBaseConnector(DataBaseRef dataBase, string rootPath, ILogger<MountedDataBaseConnector> logger) : DataBaseConnector(dataBase)
    {
        /// <inheritdoc/>
        protected override ILogger Logger => logger;

        /// <inheritdoc/>
        public override Task<string[]> GetAllFilesAsync(CancellationToken ct = default)
        {
            using (Logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.CONTROLLER] = nameof(MountedDataBaseConnector),
                    [LoggerConsts.FUNCTION] = nameof(GetAllFilesAsync)
                }))
            {
                Logger.LogDebug("Listing all files");
                string fullPath = Path.GetFullPath(Path.Combine(rootPath, DataBase.Id));
                if (!fullPath.StartsWith(rootPath))
                {
                    Logger.LogWarning("Unauthorized access attempt: The database is not in the root path.");
                    throw new UnauthorizedAccessException("The database is not in the root path");
                }

                return Task.Run(() => Directory.GetFiles(
                    fullPath,
                    $"*{PxFileConstants.FILE_ENDING}",
                    SearchOption.AllDirectories), ct);
            }
        }

        /// <inheritdoc/>
        protected override async Task<Stream> OpenPxFileStreamAsync(PxFileRef file, CancellationToken ct = default)
        {
            using (Logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.CONTROLLER] = nameof(MountedDataBaseConnector),
                    [LoggerConsts.FUNCTION] = nameof(OpenPxFileStreamAsync),
                    [LoggerConsts.PX_FILE] = file.Id
                }))
            {
                Logger.LogDebug("Opening file stream");
                if(file.DataBase.Id != DataBase.Id)
                {
                    Logger.LogWarning("The file does not belong to the database.");
                    throw new InvalidOperationException("The file does not belong to the database.");
                }

                // Use the FilePath property if it exists and points to a valid file
                if (!string.IsNullOrEmpty(file.FilePath) && File.Exists(file.FilePath))
                {
                    return await Task.Run(() => File.OpenRead(file.FilePath), ct);
                }

                // Fall back to constructing the path from components
                string path = Path.Combine(rootPath, file.DataBase.Id, file.Id);
                
                return await Task.Run(() =>
                {
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

                }, ct);
            }
        }

        /// <inheritdoc/>
        public override async Task<DateTime> GetLastWriteTimeAsync(PxFileRef file, CancellationToken ct = default)
        {
            using (Logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.CONTROLLER] = nameof(MountedDataBaseConnector),
                    [LoggerConsts.FUNCTION] = nameof(GetLastWriteTimeAsync),
                    [LoggerConsts.PX_FILE] = file.Id
                }))
            {
                Logger.LogDebug("Getting last write time");
                
                // Use the FilePath property if it exists and points to a valid file
                if (!string.IsNullOrEmpty(file.FilePath) && File.Exists(file.FilePath))
                {
                    return await Task.Run(() => File.GetLastWriteTimeUtc(file.FilePath), ct);
                }
                
                // Fall back to constructing the path from components
                string path = Path.Combine(rootPath, file.DataBase.Id, file.Id);
                return await Task.Run(() => File.GetLastWriteTimeUtc(path), ct);
            }
        }

        /// <inheritdoc/>
        public override async Task<Stream> TryReadAuxiliaryFileAsync(string relativePath, CancellationToken ct = default)
        {
            using (Logger.BeginScope(new Dictionary<string, object>
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
                    Logger.LogWarning("Aux file path escaped database root");
                    throw new UnauthorizedAccessException("Auxiliary file path escaped database root.");
                }

                return await Task.Run(() =>
                {
                    if (!File.Exists(fullPath))
                    {
                        Logger.LogWarning("Aux file {AuxFile} not found", fullPath);
                        throw new FileNotFoundException("Auxiliary file not found", fullPath);
                    }
                    return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                }, ct);
            }
        }
    }
}
