using Microsoft.Extensions.Caching.Memory;
using Px.Utils.ModelBuilders;
using Px.Utils.Models.Metadata;
using Px.Utils.PxFile.Metadata;
using PxApi.Caching;
using PxApi.Configuration;
using PxApi.ModelBuilders;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace PxApi.DataSources
{
    /// <summary>
    /// Data source for using database on the local file system.
    /// </summary>
    [ExcludeFromCodeCoverage] // This class is not unit tested because it relies on file system access.
    public class LocalFileSystemDataSource(IMemoryCache cache) : IDataSource
    {
        private readonly LocalFileSystemConfig config = AppSettings.Active.DataSource.LocalFileSystem;

        /// <inheritdoc/>
        public Task GetDatabasesAsync()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task<TablePath?> GetTablePathAsync(string database, string filename)
        {
            string rootPath = Path.Combine(config.RootPath, database);
            if (!filename.EndsWith(PxFileConstants.FILE_ENDING)) filename += PxFileConstants.FILE_ENDING;
            return Task.Run(() =>
            {
                string? filePath = Directory.EnumerateFiles(rootPath, filename, SearchOption.AllDirectories).FirstOrDefault();
                if (filePath is not null)
                {
                    if (filePath.StartsWith(rootPath)) return new TablePath(filePath);
                    else throw new UnauthorizedAccessException("The file is not in the root path");
                }
                return null;
            });
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyMatrixMetadata> GetMatrixMetadataCachedAsync(TablePath path)
        {
            string key = GetCacheEntryKey(path);
            if (cache.TryGetValue(key, out CacheItem<IReadOnlyMatrixMetadata>? metaItem) && metaItem is not null)
            {
                if (metaItem.IsFresh) return await metaItem.Task;
                else if (metaItem.FileModified == GetLastModified(path))
                {
                    cache.Set(key, new CacheItem<IReadOnlyMatrixMetadata>(metaItem));
                    return await metaItem.Task;
                }
            }
            
            Task<IReadOnlyMatrixMetadata> task = GetTableMetadataAsync(path);
            cache.Set(key, new CacheItem<IReadOnlyMatrixMetadata>(task, TimeSpan.FromSeconds(5), GetLastModified(path)));
            return await task;
        }

        /// <inheritdoc/>
        private async static Task<IReadOnlyMatrixMetadata> GetTableMetadataAsync(TablePath path)
        {
            PxFileMetadataReader reader = new();
            using FileStream fileStream = new(path.ToPathString(), FileMode.Open, FileAccess.Read, FileShare.Read);
            Encoding encoding = await reader.GetEncodingAsync(fileStream);

            if (fileStream.CanSeek) fileStream.Seek(0, SeekOrigin.Begin);
            else throw new InvalidOperationException("Not able to seek in the filestream");

            IAsyncEnumerable<KeyValuePair<string, string>> metaEntries = reader.ReadMetadataAsync(fileStream, encoding);

            MatrixMetadataBuilder builder = new();
            return await builder.BuildAsync(metaEntries);
        }

        private static DateTime GetLastModified(TablePath path)
        {
            return File.GetLastWriteTime(path.ToPathString());
        }

        private static string GetCacheEntryKey(TablePath path)
        {
            const string seed = "TABLE_PATH_SEED";
            byte[] inputBytes = Encoding.UTF8.GetBytes(path.ToPathString() + seed);
            byte[] hashBytes = MD5.HashData(inputBytes);

            return BitConverter.ToString(hashBytes);
        }
    }
}
