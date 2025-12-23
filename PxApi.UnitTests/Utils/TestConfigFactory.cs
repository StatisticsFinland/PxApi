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
                ["AllowedHosts"] = "*",
                ["Cache:MaxSizeBytes"] = "524288000",
                ["Cache:DefaultDataCellSize"] = "16",
                ["Cache:DefaultUpdateTaskSize"] = "50",
                ["Cache:DefaultTableGroupSize"] = "100",
                ["Cache:DefaultFileListSize"] = "350000",
                ["Cache:DefaultMetaSize"] = "200000",
                ["FeatureManagement:CacheController"] = "true",
                ["QueryLimits:JsonMaxCells"] = "100000",
                ["QueryLimits:JsonStatMaxCells"] = "50000",
                ["Localization:DefaultLanguage"] = "fi",
                ["Localization:SupportedLanguages:0"] = "fi",
                ["Localization:SupportedLanguages:1"] = "sv",
                ["Localization:SupportedLanguages:2"] = "en"
            };
            return config;
        }

        /// <summary>
        /// Creates configuration entries for a mounted database.
        /// </summary>
        public static Dictionary<string, string?> MountedDb(int index, string id, string? rootPath = "D:/UD/saarimaa/DataBases")
        {
            Dictionary<string, string?> config = CommonDatabaseCacheConfig(index, id, "Mounted");
            if (rootPath is not null)
            {
                config[$"DataBases:{index}:Custom:RootPath"] = rootPath;
            }
            return config;
        }

        /// <summary>
        /// Creates configuration entries for a file share database with optional storage path and share name.
        /// </summary>
        public static Dictionary<string, string?> FileShareDb(int index, string id, string? sharePath = "https://test.file.core.windows.net/", string shareName = "testshare")
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
        /// Creates configuration entries for a blob storage database with optional storage uri and container name.
        /// </summary>
        public static Dictionary<string, string?> BlobStorageDb(int index, string id, string? storagePath = "https://test.blob.core.windows.net", string? containerName = "test-container")
        {
            Dictionary<string, string?> config = CommonDatabaseCacheConfig(index, id, "BlobStorage");
            if (storagePath is not null)
            {
                config[$"DataBases:{index}:Custom:StoragePath"] = storagePath;
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
