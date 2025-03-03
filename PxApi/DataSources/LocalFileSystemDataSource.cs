using Microsoft.Extensions.Caching.Memory;
using Px.Utils.Language;
using Px.Utils.ModelBuilders;
using Px.Utils.Models.Metadata;
using Px.Utils.PxFile.Metadata;
using PxApi.Configuration;
using PxApi.ModelBuilders;
using PxApi.Models;
using PxApi.Utilities;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;

namespace PxApi.DataSources
{
    /// <summary>
    /// Data source for using database on the local file system.
    /// </summary>
    [ExcludeFromCodeCoverage] // This class is not unit tested because it relies on file system access.
    public class LocalFileSystemDataSource : IDataSource
    {
        private readonly LocalFileSystemConfig config = AppSettings.Active.DataSource.LocalFileSystem;
        private readonly FileSystemWatcher watcher = new();
        private readonly IMemoryCache _cache;
        private readonly ILogger<LocalFileSystemDataSource> _logger;

        /// <summary>
        /// Default constructor, initializes the data source and starts tracking changes.
        /// </summary>
        public LocalFileSystemDataSource(IMemoryCache cache, ILogger<LocalFileSystemDataSource> logger)
        {
            _logger = logger;
            _cache = cache;

            watcher.Path = config.RootPath;
            watcher.IncludeSubdirectories = true;
            watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;
            watcher.Filter = "*.px";
            watcher.Changed += FileModified;
            watcher.Deleted += FileDeleted;
            watcher.Created += FileCreated;
            watcher.EnableRaisingEvents = true;
        }

        /// <inheritdoc/>
        public Task GetDatabasesAsync()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public async Task<PxTable> GetTablePathAsync(string database, string filename)
        {
            ImmutableSortedDictionary<string, PxTable> pathDict = await GetSortedTableDictCachedAsync(database);
            if (filename.EndsWith(PxFileConstants.FILE_ENDING)) filename = filename[..^PxFileConstants.FILE_ENDING.Length];
            if (pathDict.TryGetValue(filename, out PxTable? path) && path is not null) return path;
            else throw new FileNotFoundException("The file was not found in the database");
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyMatrixMetadata> GetMatrixMetadataCachedAsync(PxTable path)
        {
            string key = GenerateTableCacheKey(path);
            if (_cache.TryGetValue(key, out Task<IReadOnlyMatrixMetadata>? metaTask) && metaTask is not null)
            {
                return await metaTask;
            }
            else
            {
                Task<IReadOnlyMatrixMetadata> newTask = GetTableMetadataAsync(path);
                MemoryCacheEntryOptions options = new()
                {
                    SlidingExpiration = config.MetadataCache.SlidingExpirationMinutes,
                    AbsoluteExpirationRelativeToNow = config.MetadataCache.AbsoluteExpirationMinutes,
                    Priority = CacheItemPriority.Normal,
                    Size = 20
                };

                return await _cache.Set(key, newTask, options);
            }
        }

        /// <inheritdoc/>
        public async Task<string> GetSingleStringValueFromTable(string key, PxTable path)
        {
            PxFileMetadataReader reader = new();
            using FileStream fileStream = new(path.GetFullPathToTable(config.RootPath), FileMode.Open, FileAccess.Read, FileShare.Read);
            Encoding encoding = await reader.GetEncodingAsync(fileStream);

            if (fileStream.CanSeek) fileStream.Seek(0, SeekOrigin.Begin);
            else throw new InvalidOperationException("Not able to seek in the filestream");

            IAsyncEnumerable<KeyValuePair<string, string>> metaEntries = reader.ReadMetadataAsync(fileStream, encoding);

            return (await metaEntries.FirstAsync(pair => pair.Key == key)).Value;
        }

        /// <inheritdoc/>
        public async Task<ImmutableSortedDictionary<string, PxTable>> GetSortedTableDictCachedAsync(string dbId)
        {
            string key = GenerateDatabaseListingCacheKey(dbId);
            if (_cache.TryGetValue(key, out Task<ImmutableSortedDictionary<string, PxTable>>? sortedTables))
            {
                return await (sortedTables ?? throw new InvalidOperationException("The task for getting the sortet table list found in cache was null"));
            }
            else
            {
                string fullPath = Path.GetFullPath(Path.Combine(config.RootPath, dbId));
                if(!fullPath.StartsWith(config.RootPath)) throw new UnauthorizedAccessException("The database is not in the root path");
                Task<ImmutableSortedDictionary<string, PxTable>> sortedDict = 
                    Task.Run(() => Directory.GetFiles(
                    fullPath,
                    $"*{PxFileConstants.FILE_ENDING}",
                    SearchOption.AllDirectories)
                    .ToImmutableSortedDictionary(
                        path => Path.GetFileNameWithoutExtension(path),
                        path => PathFunctions.BuildTableReferenceFromPath(path, config.RootPath)
                    ));

                MemoryCacheEntryOptions options = new()
                {
                    SlidingExpiration = TimeSpan.FromMinutes(30),
                    Priority = CacheItemPriority.High,
                    Size = 5
                };

                return await _cache.Set(key, sortedDict, options);
            }
        }

        /// <inheritdoc/>
        public async Task<List<TableGroup>> GetTableGroupingCachedAsync(PxTable table, string lang)
        {
            string key = GenerateGroupingsCacheKey(table);
            if (!_cache.TryGetValue(key, out Task<List<GroupingCacheItem>>? cachedGroups) || cachedGroups is not null)
            {
                MemoryCacheEntryOptions options = new()
                {
                    SlidingExpiration = TimeSpan.FromMinutes(15),
                    Priority = CacheItemPriority.Low,
                    Size = 1
                };
                cachedGroups = _cache.Set(key, GetTableGroupingAsync(table), options);
            }

            if(cachedGroups is null) throw new InvalidOperationException("The task for getting the table groupings was null");
            return (await cachedGroups).Select(g => new TableGroup()
            {
                Code = g.Code,
                Name = g.Name[lang],
                GroupingCode = g.GroupingCode,
                GroupingName = g.GroupingName[lang],
                Links = []
            }).ToList();
        }

        private async Task<IReadOnlyMatrixMetadata> GetTableMetadataAsync(PxTable path)
        {
            PxFileMetadataReader reader = new();
            using FileStream fileStream = new(path.GetFullPathToTable(config.RootPath), FileMode.Open, FileAccess.Read, FileShare.Read);
            Encoding encoding = await reader.GetEncodingAsync(fileStream);

            if (fileStream.CanSeek) fileStream.Seek(0, SeekOrigin.Begin);
            else throw new InvalidOperationException("Not able to seek in the filestream");

            IAsyncEnumerable<KeyValuePair<string, string>> metaEntries = reader.ReadMetadataAsync(fileStream, encoding);

            MatrixMetadataBuilder builder = new();
            MatrixMetadata meta = await builder.BuildAsync(metaEntries);
            MatrixMetadataUtilityFunctions.AssignOrdinalDimensionTypes(meta);

            return meta;
        }

        private static string GenerateTableCacheKey(PxTable path)
        {
            const string seed = "TABLE_PATH_SEED";
            string hierarchy = string.Join('-', path.Hierarchy);
            byte[] inputBytes = Encoding.UTF8.GetBytes(string.Join('-', path.DatabaseId, hierarchy, path.TableId, seed));
            byte[] hashBytes = MD5.HashData(inputBytes);

            return BitConverter.ToString(hashBytes);
        }

        private static string GenerateGroupingsCacheKey(PxTable path)
        {
            const string seed = "TABLE_GROUPINGS_SEED";
            // The key is per grouping here, not per table.
            string hierarchy = string.Join('-', path.Hierarchy);
            byte[] inputBytes = Encoding.UTF8.GetBytes(string.Join('-', path.DatabaseId, hierarchy, seed));
            byte[] hashBytes = MD5.HashData(inputBytes);

            return BitConverter.ToString(hashBytes);
        }

        private static string GenerateDatabaseListingCacheKey(string dbId)
        {
            const string seed = "DB_TABLE_LISTING";
            byte[] inputBytes = Encoding.UTF8.GetBytes($"{dbId}-{seed}");
            byte[] hashBytes = MD5.HashData(inputBytes);

            return BitConverter.ToString(hashBytes);
        }

        private async Task<List<GroupingCacheItem>> GetTableGroupingAsync(PxTable table)
        {
            List<GroupingCacheItem> groups = [];
            string path = Path.Combine(config.RootPath, table.DatabaseId);

            foreach (string hierarchy in table.Hierarchy)
            {
                string groupingFilePath = Path.Combine(path, "groupings.json");
                if (File.Exists(groupingFilePath))
                {
                    string goupingJson = await GetFileContents(groupingFilePath);
                    Groupings groupings = JsonSerializer.Deserialize<Groupings>(goupingJson, GlobalJsonConverterOptions.Default)
                        ?? throw new InvalidOperationException("The grouping file was not valid JSON");

                    path = Path.Combine(path, hierarchy);
                    groups.Add(new GroupingCacheItem()
                    {
                        Code = hierarchy,
                        Name = await GetAlias(path),
                        GroupingCode = groupings.Code,
                        GroupingName = groupings.Name
                    });
                }
            }

            return groups;
        }

        private static async Task<MultilanguageString> GetAlias(string path)
        {
            Dictionary<string, string> translatedNames = [];
            IEnumerable<string> aliasFiles = Directory.GetFiles(path, FilesystemDatasourceConstants.ALIAS_FILE_PREFIX + "*.txt");
            foreach (string aliasFile in aliasFiles)
            {
                string lang = Path.GetFileNameWithoutExtension(aliasFile)[FilesystemDatasourceConstants.ALIAS_FILE_PREFIX.Length..];
                string alias = await GetFileContents(aliasFile);
                translatedNames.Add(lang, alias.Trim());
            }
            return new MultilanguageString(translatedNames);
        }

        private static async Task<string> GetFileContents(string path)
        {
            using FileStream? fs = File.OpenRead(path);
            Encoding defaultEncoding = Encoding.Default;
            Ude.CharsetDetector cdet = new();
            cdet.Feed(fs);
            cdet.DataEnd();
            if (cdet.Charset != null)
            {
                defaultEncoding = Encoding.GetEncoding(cdet.Charset);
            }
            fs.Position = 0;
            using StreamReader sr = new(fs, defaultEncoding);
            return await sr.ReadToEndAsync();
        }

        private void FileModified(object sender, FileSystemEventArgs e)
        {
            _logger.LogDebug("Database handler detected a file change: {Path}", e.Name);
            PxTable table = PathFunctions.BuildTableReferenceFromPath(e.FullPath, config.RootPath);
            _cache.Remove(GenerateTableCacheKey(table));
        }

        private void FileDeleted(object sender, FileSystemEventArgs e)
        {
            _logger.LogDebug("Database handler detected a file deletion: {Path}", e.Name);
            PxTable table = PathFunctions.BuildTableReferenceFromPath(e.FullPath, config.RootPath);
            _cache.Remove(GenerateTableCacheKey(table));
            _cache.Remove(GenerateGroupingsCacheKey(table));
            _cache.Remove(GenerateDatabaseListingCacheKey(table.DatabaseId));
        }

        private void FileCreated(object sender, FileSystemEventArgs e)
        {
            _logger.LogDebug("Database handler detected a new file: {Path}", e.Name);
            PxTable table = PathFunctions.BuildTableReferenceFromPath(e.FullPath, config.RootPath);
            _cache.Remove(GenerateDatabaseListingCacheKey(table.DatabaseId));
        }
    }
}
