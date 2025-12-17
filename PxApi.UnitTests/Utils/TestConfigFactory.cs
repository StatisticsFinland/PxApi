using Microsoft.Extensions.Configuration;
using PxApi.Configuration;

namespace PxApi.UnitTests.Utils
{
    /// <summary>
    /// Factory utilities for building test configuration dictionaries in a composable way.
    /// </summary>
    public static class TestConfigFactory
    {
        /// <summary>
        /// Creates the base configuration containing root url and localization settings.
        /// </summary>
        public static Dictionary<string, string?> Base()
        {
            Dictionary<string, string?> config = new()
            {
                ["RootUrl"] = "https://testurl.fi",
                ["Localization:DefaultLanguage"] = "fi",
                ["Localization:SupportedLanguages:0"] = "fi",
                ["Localization:SupportedLanguages:1"] = "sv",
                ["Localization:SupportedLanguages:2"] = "en"
            };
            return config;
        }

        /// <summary>
        /// Creates configuration entries for a mounted database with optional root path.
        /// If rootPath is null the key is omitted. If empty string is provided it is added with empty value.
        /// </summary>
        public static Dictionary<string, string?> MountedDb(int index, string id, string? rootPath = "/test/root/path")
        {
            Dictionary<string, string?> config = CommonDatabaseCacheConfig(index, id, "Mounted");
            if (rootPath is not null)
            {
                config[$"DataBases:{index}:Custom:RootPath"] = rootPath;
            }
            return config;
        }

        /// <summary>
        /// Creates configuration entries for a file share database with optional share path.
        /// </summary>
        public static Dictionary<string, string?> FileShareDb(int index, string id, string? sharePath = "//storage/path", string? shareName = "sharename")
        {
            Dictionary<string, string?> config = CommonDatabaseCacheConfig(index, id, "FileShare");
            if (sharePath is not null)
            {
                config[$"DataBases:{index}:Custom:StoragePath"] = sharePath;
                config[$"DataBases:{index}:Custom:ShareName"] = shareName;
            }
            return config;
        }

        /// <summary>
        /// Creates configuration entries for a blob storage database with optional connection string and container name.
        /// </summary>
        public static Dictionary<string, string?> BlobStorageDb(int index, string id, string? connectionString = "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=key;", string? containerName = "test-container")
        {
            Dictionary<string, string?> config = CommonDatabaseCacheConfig(index, id, "BlobStorage");
            if (connectionString is not null)
            {
                config[$"DataBases:{index}:Custom:ConnectionString"] = connectionString;
            }
            if (containerName is not null)
            {
                config[$"DataBases:{index}:Custom:ContainerName"] = containerName;
            }
            return config;
        }

        /// <summary>
        /// Merges multiple configuration dictionaries. Later dictionaries override earlier keys.
        /// </summary>
        public static Dictionary<string, string?> Merge(params Dictionary<string, string?>[] dictionaries)
        {
            Dictionary<string, string?> merged = [];
            foreach (Dictionary<string, string?> dict in dictionaries)
            {
                foreach (KeyValuePair<string, string?> kvp in dict)
                {
                    merged[kvp.Key] = kvp.Value;
                }
            }
            return merged;
        }

        /// <summary>
        /// Builds an IConfiguration from the provided merged dictionary and loads AppSettings.
        /// </summary>
        public static IConfiguration BuildAndLoad(Dictionary<string, string?> config)
        {
            IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();
            AppSettings.Load(configuration);
            return configuration;
        }

        private static Dictionary<string, string?> CommonDatabaseCacheConfig(int index, string id, string type)
        {
            Dictionary<string, string?> config = new()
            {
                [$"DataBases:{index}:Type"] = type,
                [$"DataBases:{index}:Id"] = id,
                [$"DataBases:{index}:CacheConfig:TableList:SlidingExpirationSeconds"] = "900",
                [$"DataBases:{index}:CacheConfig:TableList:AbsoluteExpirationSeconds"] = "900",
                [$"DataBases:{index}:CacheConfig:Meta:SlidingExpirationSeconds"] = "900",
                [$"DataBases:{index}:CacheConfig:Meta:AbsoluteExpirationSeconds"] = "900",
                [$"DataBases:{index}:CacheConfig:Groupings:SlidingExpirationSeconds"] = "900",
                [$"DataBases:{index}:CacheConfig:Groupings:AbsoluteExpirationSeconds"] = "900",
                [$"DataBases:{index}:CacheConfig:Data:SlidingExpirationSeconds"] = "600",
                [$"DataBases:{index}:CacheConfig:Data:AbsoluteExpirationSeconds"] = "600"
            };
            return config;
        }
    }
}
