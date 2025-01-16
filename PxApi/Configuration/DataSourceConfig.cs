
namespace PxApi.Configuration
{
    public class DataSourceConfig
    {
        public LocalFileSystemConfig LocalFileSystem { get; }

        public DataSourceConfig(IConfigurationSection section)
        {
            LocalFileSystem = new LocalFileSystemConfig(section.GetRequiredSection(nameof(LocalFileSystem)));
        }
    }
}
