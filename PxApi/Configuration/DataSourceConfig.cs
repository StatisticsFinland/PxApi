
namespace PxApi.Configuration
{
    /// <summary>
    /// Holds configuration for a datasource
    /// </summary>
    public class DataSourceConfig
    {
        /// <summary>
        /// Configuration for the local file system that is used as a datasource
        /// </summary>
        public LocalFileSystemConfig LocalFileSystem { get; }

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="section">Section of the application configuration that contains settings for the data source.</param>
        public DataSourceConfig(IConfigurationSection section)
        {
            LocalFileSystem = new LocalFileSystemConfig(section.GetRequiredSection(nameof(LocalFileSystem)));
        }
    }
}
