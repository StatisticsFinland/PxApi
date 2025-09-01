using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Metadata;
using PxApi.DataSources;
using PxApi.Models;
using System.Collections.Immutable;

namespace PxApi.Caching
{
    /// <summary>
    /// Defines methods for retrieving data, file lists, and metadata from a PX file data source,  with built-in caching
    /// to optimize performance for repeated requests.
    /// </summary>
    public interface ICachedDataSource
    {
        /// <summary>
        /// Retrieves an array of <see cref="DoubleDataValue"/> objects based on the specified PX file and matrix map, 
        /// utilizing caching to improve performance for repeated requests.
        /// </summary>
        /// <param name="pxFile">The PX file containing the data to be processed. Cannot be null.</param>
        /// <param name="map">The matrix map that defines the structure and mapping of the data. Cannot be null.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains an array of  <see
        /// cref="DoubleDataValue"/> objects representing the processed data. The array will be empty if no data is
        /// available.</returns>
        Task<DoubleDataValue[]> GetDataCachedAsync(PxFileRef pxFile, IMatrixMap map);

        /// <summary>
        /// Retrieves a cached list of files associated with the specified database.
        /// </summary>
        /// <param name="dataBase">The database for which to retrieve the file list. Cannot be null.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains an immutable list of  <see
        /// cref="PxFileRef"/> objects associated with the specified database. The list will be empty if no files are
        /// found.</returns>
        Task<ImmutableSortedDictionary<string, PxFileRef>> GetFileListCachedAsync(DataBaseRef dataBase);

        /// <summary>
        /// Retrieves a reference to a database object based on the specified database identifier.
        /// </summary>
        /// <remarks>Use this method to obtain a reference to a database for further operations. Ensure
        /// the provided identifier corresponds to an existing database.</remarks>
        /// <param name="dbId">The unique identifier of the database to retrieve. Cannot be null or empty.</param>
        /// <returns>A <see cref="DataBaseRef"/> object representing the database associated with the specified identifier. Returns
        /// <see langword="null"/> if no database matches the provided identifier.</returns>
        DataBaseRef? GetDataBaseReference(string dbId);

        /// <summary>
        /// Retrieves references to all available databases.
        /// </summary>
        /// <returns>A read-only collection of all available databases.</returns>
        IReadOnlyCollection<DataBaseRef> GetAllDataBaseReferences();

        /// <summary>
        /// Retrieves a cached reference to a file based on its unique identifier.
        /// </summary>
        /// <remarks>This method retrieves the file reference from a cache if available; otherwise, it
        /// queries the database. Ensure that the provided <paramref name="fileId"/> is valid and corresponds to an
        /// existing file.</remarks>
        /// <param name="fileId">The unique identifier of the file to retrieve. Cannot be null or empty.</param>
        /// <param name="db">The database instance used to query the file reference. Cannot be null.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the <see cref="PxFileRef"/> object 
        /// corresponding to the specified <paramref name="fileId"/>.</returns>
        Task<PxFileRef?> GetFileReferenceCachedAsync(string fileId, DataBaseRef db);

        /// <summary>
        /// Asynchronously retrieves the metadata for the specified PX file, utilizing caching to improve performance.
        /// </summary>
        /// <param name="pxFile">The PX file for which metadata is to be retrieved. Cannot be null.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the metadata for the specified
        /// PX file as an <see cref="IReadOnlyMatrixMetadata"/> object.</returns>
        Task<IReadOnlyMatrixMetadata> GetMetadataCachedAsync(PxFileRef pxFile);

        /// <summary>
        /// Asynchronously retrieves a single string value associated with the specified key from the provided file.
        /// </summary>
        /// <param name="key">The key used to locate the string value within the file. Cannot be null or empty.</param>
        /// <param name="file">The file from which the string value is retrieved. Must be a valid <see cref="PxFileRef"/> instance.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the string value associated with
        /// the specified key.</returns>
        Task<string> GetSingleStringValueAsync(string key, PxFileRef file);

        /// <summary>
        /// Clears all file list cache entries.
        /// </summary>
        /// <param name="dbRef">Reference to the database to clear the file list cache for</param>
        void ClearFileListCache(DataBaseRef dbRef);

        /// <summary>
        /// Clears all metadata cache entries for the specified database.
        /// </summary>
        /// <param name="dataBase">The database for which to clear metadata cache entries.</param>
        Task ClearMetadataCacheAsync(DataBaseRef dataBase);

        /// <summary>
        /// Clears all data cache entries for the specified database.
        /// </summary>
        /// <param name="dataBase">The database for which to clear data cache entries.</param>
        Task ClearDataCacheAsync(DataBaseRef dataBase);

        /// <summary>
        /// Clears last updated timestamp cache entries for the specified database.
        /// </summary>
        /// <param name="dataBase">The database for which to clear last updated timestamp cache entries.</param>
        Task ClearLastUpdatedCacheAsync(DataBaseRef dataBase);

        /// <summary>
        /// Clears all cache entries for the specified database, including file lists, metadata, and data.
        /// </summary>
        /// <param name="dataBase">The database for which to clear all cache entries.</param>
        Task ClearAllCache(DataBaseRef dataBase);

        /// <summary>
        /// Clears all cache entries for all databases.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task ClearAllCachesAsync();
    }
}