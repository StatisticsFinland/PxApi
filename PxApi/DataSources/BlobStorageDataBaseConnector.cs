using PxApi.Models;
using PxApi.Utilities;
using System.Diagnostics.CodeAnalysis;

namespace PxApi.DataSources
{
    /// <summary>
    /// Data source for using database in Azure Blob Storage.
    /// </summary>
    [ExcludeFromCodeCoverage] // This class is not implemented yet.
    public class BlobStorageDataBaseConnector : IDataBaseConnector
    {
        private readonly DataBaseRef _dataBase; 
        private readonly string _connectionString;
        private readonly string _containerName;
        private readonly ILogger<BlobStorageDataBaseConnector> _logger;

        /// <inheritdoc/>
        public DataBaseRef DataBase => _dataBase;

        /// <summary>
        /// Initializes a new instance of the <see cref="BlobStorageDataBaseConnector"/> class.
        /// </summary>
        /// <param name="dataBase">The database ID.</param>
        /// <param name="connectionString">Azure Storage connection string.</param>
        /// <param name="containerName">Blob container name.</param>
        /// <param name="logger">Logger for the connector.</param>
        public BlobStorageDataBaseConnector(DataBaseRef dataBase, string connectionString, string containerName, ILogger<BlobStorageDataBaseConnector> logger)
        {
            _dataBase = dataBase;
            _connectionString = connectionString;
            _containerName = containerName;
            _logger = logger;
        }

        /// <inheritdoc/>
        public Task<string[]> GetAllFilesAsync()
        {
            using (_logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.CLASS_NAME] = nameof(BlobStorageDataBaseConnector),
                    [LoggerConsts.METHOD_NAME] = nameof(GetAllFilesAsync)
                }))
            {
                _logger.LogDebug("GetAllFilesAsync not implemented yet");
                throw new NotImplementedException("BlobStorage database connector is not implemented yet.");
            }
        }

        /// <inheritdoc/>
        public Stream ReadPxFile(PxFileRef file)
        {
            using (_logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.CLASS_NAME] = nameof(BlobStorageDataBaseConnector),
                    [LoggerConsts.METHOD_NAME] = nameof(ReadPxFile),
                    [LoggerConsts.PX_FILE] = file.Id
                }))
            {
                _logger.LogDebug("ReadPxFile not implemented yet");
                throw new NotImplementedException("BlobStorage database connector is not implemented yet.");
            }
        }

        /// <inheritdoc/>
        public Task<DateTime> GetLastWriteTimeAsync(PxFileRef file)
        {
            using (_logger.BeginScope(
                new Dictionary<string, object>
                {
                    [LoggerConsts.DB_ID] = DataBase.Id,
                    [LoggerConsts.CLASS_NAME] = nameof(BlobStorageDataBaseConnector),
                    [LoggerConsts.METHOD_NAME] = nameof(GetLastWriteTimeAsync),
                    [LoggerConsts.PX_FILE] = file.Id
                }))
            {
                _logger.LogDebug("GetLastWriteTimeAsync not implemented yet");
                throw new NotImplementedException("BlobStorage database connector is not implemented yet.");
            }
        }
    }
}