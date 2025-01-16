using System.ComponentModel.Design;

namespace PxApi.Configuration
{
    public class LocalFileSystemConfig
    {
        public string RootPath { get; }

        public LocalFileSystemConfig(IConfigurationSection section)
        {
            RootPath = section.GetValue<string>(nameof(RootPath))
                ?? throw new InvalidOperationException($"Missing required configuration value: {nameof(RootPath)}");
        }
    }
}
