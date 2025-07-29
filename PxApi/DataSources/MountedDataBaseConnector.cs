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
                    [LoggerConsts.CLASS_NAME] = nameof(MountedDataBaseConnector),
                    [LoggerConsts.METHOD_NAME] = nameof(GetAllFilesAsync)
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
        public Stream ReadPxFile(PxFileRef file)
        {
            using (logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.CLASS_NAME] = nameof(MountedDataBaseConnector),
                    [LoggerConsts.METHOD_NAME] = nameof(ReadPxFile),
                    [LoggerConsts.PX_FILE] = file.Id
                }))
            {
                logger.LogDebug("Opening file stream");
                if(file.DataBase.Id != DataBase.Id)
                {
                    logger.LogWarning("The file does not belong to the database.");
                    throw new InvalidOperationException("The file does not belong to the database.");
                }
                string path = Path.Combine(rootPath, file.DataBase.Id, file.Id);
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
                    [LoggerConsts.CLASS_NAME] = nameof(MountedDataBaseConnector),
                    [LoggerConsts.METHOD_NAME] = nameof(GetLastWriteTimeAsync),
                    [LoggerConsts.PX_FILE] = file.Id
                }))
            {
                logger.LogDebug("Getting last write time");
                string path = Path.Combine(rootPath, file.DataBase.Id, file.Id);
                return await Task.Run(() => File.GetLastWriteTimeUtc(path));
            }
        }
    }
}
