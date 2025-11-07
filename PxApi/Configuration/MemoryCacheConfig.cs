namespace PxApi.Configuration
{
    /// <summary>
    /// Configuration settings for global cache management.
    /// </summary>
    public class MemoryCacheConfig
    {
        /// <summary>
        /// The maximum size of the global cache in bytes. Default is 512 MB.
        /// </summary>
        public long MaxSizeBytes { get; }

        /// <summary>
        /// Default size for data cell cache items. Default is 16 bytes.
        /// </summary>
        public int DefaultDataCellSize { get; }

        /// <summary>
        /// Default size for update task cache items. Default is 50 bytes.
        /// </summary>
        public int DefaultUpdateTaskSize { get; }

        /// <summary>
        /// Default size for table group cache items. Default is 100 bytes.
        /// </summary>
        public int DefaultTableGroupSize { get; }

        /// <summary>
        /// Default size for file list cache items. Default is 350000 bytes.
        /// </summary>
        public int DefaultFileListSize { get; }

        /// <summary>
        /// Default size for metadata cache items. Default is 200000 bytes.
        /// </summary>
        public int DefaultMetaSize { get; }

        /// <summary>
        /// Initializes a new instance of the MemoryCacheConfig class.
        /// </summary>
        /// <param name="section">Configuration section that contains the cache settings.</param>
        public MemoryCacheConfig(IConfigurationSection section)
        {
            long maxSizeBytes = 524288000; // 512 MB default
            MaxSizeBytes = section.GetValue<long>(nameof(MaxSizeBytes), maxSizeBytes);

            DefaultDataCellSize = section.GetValue<int>(nameof(DefaultDataCellSize), 16);
            DefaultUpdateTaskSize = section.GetValue<int>(nameof(DefaultUpdateTaskSize), 50);
            DefaultTableGroupSize = section.GetValue<int>(nameof(DefaultTableGroupSize), 100);
            DefaultFileListSize = section.GetValue<int>(nameof(DefaultFileListSize), 350000);
            DefaultMetaSize = section.GetValue<int>(nameof(DefaultMetaSize), 200000);
        }
    }
}