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
    [ExcludeFromCodeCoverage] // This class is not implemented yet.
    public class FileShareDataBaseConnector(DataBaseRef dataBase, string sharePath, ILogger<FileShareDataBaseConnector> logger) : IDataBaseConnector
    {
        /// <inheritdoc/>
        public DataBaseRef DataBase => _dataBase;

        private readonly DataBaseRef _dataBase = dataBase;
        private readonly string _sharePath = sharePath;
        private readonly ILogger<FileShareDataBaseConnector> _logger = logger;

        /// <inheritdoc/>
        public Task<string[]> GetAllFilesAsync()
        {
            using (_logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.CLASS_NAME] = nameof(FileShareDataBaseConnector),
                    [LoggerConsts.METHOD_NAME] = nameof(GetAllFilesAsync)
                }))
            {
                _logger.LogDebug("GetAllFilesAsync not implemented yet");
                throw new NotImplementedException("FileShare database connector is not implemented yet.");
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
                _logger.LogDebug("ReadPxFile not implemented yet");
                throw new NotImplementedException("FileShare database connector is not implemented yet.");
            }
        }

        /// <inheritdoc/>
        public Task<DateTime> GetLastWriteTimeAsync(PxFileRef file)
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
                _logger.LogDebug("GetLastWriteTimeAsync not implemented yet");
                throw new NotImplementedException("FileShare database connector is not implemented yet.");

            }
        }
    }
}