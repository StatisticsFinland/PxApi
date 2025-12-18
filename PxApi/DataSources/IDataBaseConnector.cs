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
        /// Get paths of all Px files in the database.
        /// </summary>
        /// <returns>Task that resolves to an array of file paths.</returns>
        public Task<string[]> GetAllFilesAsync();

        /// <summary>
        /// Reads a Px file and returns a stream to access its contents.
        /// </summary>
        /// <param name="file"><see cref="PxFileRef"/> reference to the Px file.</param>
        /// <returns>Stream to read the contents of the Px file.</returns>
        public Task<Stream> ReadPxFileAsync(PxFileRef file);

        /// <summary>
        /// Gets the last write time of a Px file.
        /// </summary>
        /// <param name="file"><see cref="PxFileRef"/> reference to the Px file.</param>
        /// <returns>A task that resolves to the last write time of the file.</returns>
        public Task<DateTime> GetLastWriteTimeAsync(PxFileRef file);

        /// <summary>
        /// Opens an auxiliary (non PX) file located in the database root or a sub directory.
        /// Used for reading grouping metadata (e.g. groupings.json and Alias_{lang}.txt files).
        /// Throws <see cref="FileNotFoundException"/> if the file does not exist and <see cref="UnauthorizedAccessException"/> if the resolved path escapes the database root.
        /// </summary>
        /// <param name="relativePath">Path relative to the database root. Uses '/' as separator.</param>
        /// <returns>A task resolving to an open readable <see cref="Stream"/>. Never null.</returns>
        /// <exception cref="FileNotFoundException">If the file does not exist.</exception>
        /// <exception cref="UnauthorizedAccessException">If the path resolves outside the database root.</exception>
        public Task<Stream> TryReadAuxiliaryFileAsync(string relativePath);
    }
}
