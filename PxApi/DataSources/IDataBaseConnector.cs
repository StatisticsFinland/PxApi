using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Metadata;
using PxApi.Models;

namespace PxApi.DataSources
{
    /// <summary>
    /// Interface for different IO connectors to access a database of Px files.
    /// </summary>
    public interface IDataBaseConnector
    {
        /// <summary>
        /// <see cref="DataBaseRef"/> reference to the database this connector is associated with.
        /// </summary>
        public DataBaseRef DataBase { get; }

        /// <summary>
        /// Performs a lightweight connectivity check to verify that the underlying data source is reachable.
        /// Throws if the connection cannot be established.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A task representing the asynchronous check operation.</returns>
        public Task CheckConnectionAsync(CancellationToken ct = default);

        /// <summary>
        /// Get references to all Px files available in the database.
        /// </summary>
        /// <returns>Task that resolves to an array of <see cref="PxFileRef"/> references.</returns>
        public Task<PxFileRef[]> GetAllFilesAsync(CancellationToken ct = default);

        /// <summary>
        /// Gets the last write time of a Px file.
        /// </summary>
        /// <param name="file"><see cref="PxFileRef"/> reference to the Px file.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A task that resolves to the last write time of the file.</returns>
        public Task<DateTime> GetLastWriteTimeAsync(PxFileRef file, CancellationToken ct = default);

        /// <summary>
        /// Reads metadata of a Px file.
        /// </summary>
        /// <param name="file">Reference to the Px file.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A task that resolves to the matrix metadata.</returns>
        public Task<IReadOnlyMatrixMetadata> ReadMetadataAsync(PxFileRef file, CancellationToken ct = default);

        /// <summary>
        /// Reads the data values from a Px file.
        /// </summary>
        /// <param name="file">Reference to the Px file.</param>
        /// <param name="targetMap">Metadata structure of the data to read.</param>
        /// <param name="fileMeta">Complete metadata structure of the Px file.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Array of <see cref="DoubleDataValue"/> containing the data values.</returns>
        public Task<DoubleDataValue[]> ReadDataAsync(PxFileRef file, IMatrixMap targetMap, IReadOnlyMatrixMetadata fileMeta, CancellationToken ct = default);

        /// <summary>
        /// Opens an auxiliary (non PX) file located in the database root or a sub directory.
        /// Used for reading grouping metadata (e.g. groupings.json and Alias_{lang}.txt files).
        /// Throws <see cref="FileNotFoundException"/> if the file does not exist and <see cref="UnauthorizedAccessException"/> if the resolved path escapes the database root.
        /// </summary>
        /// <param name="fileName">The name of the auxiliary file to read.</param>
        /// <param name="hierarchy">Optional array of folder names representing the hierarchy under which the file is located, relative to the database root. Can be null or empty if the file is located directly under the database root.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A task resolving to an open readable <see cref="Stream"/>. Never null.</returns>
        /// <exception cref="FileNotFoundException">If the file does not exist.</exception>
        /// <exception cref="UnauthorizedAccessException">If the path resolves outside the database root.</exception>
        public Task<Stream> TryReadAuxiliaryFileAsync(string fileName, string[]? hierarchy, CancellationToken ct = default);
    }
}
