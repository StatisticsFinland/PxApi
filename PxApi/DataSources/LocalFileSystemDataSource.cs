using Px.Utils.Language;
using Px.Utils.ModelBuilders;
using Px.Utils.Models.Metadata;
using Px.Utils.Models.Metadata.Dimensions;
using Px.Utils.Models.Metadata.Enums;
using Px.Utils.Models.Metadata.MetaProperties;
using Px.Utils.PxFile.Metadata;
using PxApi.Configuration;
using PxApi.ModelBuilders;
using PxApi.Utilities;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace PxApi.DataSources
{
    /// <summary>
    /// Data source for using database on the local file system.
    /// </summary>
    [ExcludeFromCodeCoverage] // This class is not unit tested because it relies on file system access.
    public class LocalFileSystemDataSource() : IDataSource
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
            if(!filename.EndsWith(PxFileConstants.FILE_ENDING)) filename += PxFileConstants.FILE_ENDING;
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
        public async Task<IReadOnlyMatrixMetadata> GetTableMetadataAsync(TablePath path)
        {
            PxFileMetadataReader reader = new();
            using FileStream fileStream = new(path.ToPathString(), FileMode.Open, FileAccess.Read, FileShare.Read);
            Encoding encoding = await reader.GetEncodingAsync(fileStream);

            if (fileStream.CanSeek) fileStream.Seek(0, SeekOrigin.Begin);
            else throw new InvalidOperationException("Not able to seek in the filestream");

            IAsyncEnumerable<KeyValuePair<string, string>> metaEntries = reader.ReadMetadataAsync(fileStream, encoding);
            
            MatrixMetadataBuilder builder = new();
            MatrixMetadata meta = await builder.BuildAsync(metaEntries);
            MatrixMetadataUtilityFunctions.AssignOrdinalDimensionTypes(meta);

            return meta;
        }
    }
}
