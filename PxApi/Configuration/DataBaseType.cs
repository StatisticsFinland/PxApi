namespace PxApi.Configuration
{
    /// <summary>
    /// Different types of databases supported by the API.
    /// </summary>
    public enum DataBaseType
    {
        /// <summary>
        /// Mounted database, typically used for local or network file systems.
        /// </summary>
        Mounted,
        /// <summary>
        /// Database accessed via Microsoft Azure File Share API.
        /// </summary>
        FileShare,
        /// <summary>
        /// Database accessed via Microsoft Azure Blob Storage API.
        /// </summary>
        BlobStorage,
    }
}
