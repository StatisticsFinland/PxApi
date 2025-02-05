using Px.Utils.Language;
using Px.Utils.ModelBuilders;
using Px.Utils.Models.Metadata;
using Px.Utils.Models.Metadata.Dimensions;
using Px.Utils.Models.Metadata.Enums;
using Px.Utils.Models.Metadata.MetaProperties;
using Px.Utils.PxFile.Metadata;
using PxApi.Configuration;
using PxApi.ModelBuilders;
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
            AssignOrdinalDimensionTypes(meta);

            return meta;
        }

        // TODO: Move to a separate class for testing
        /// <summary>
        /// Assigns ordinal or nominal dimension types to dimensions that are either of unknown or other type based on their meta-id properties.
        /// </summary>
        /// <param name="meta">The matrix metadata to assign the dimension types to.</param>
        private static void AssignOrdinalDimensionTypes(MatrixMetadata meta)
        {
            for (int i = 0; i < meta.Dimensions.Count; i++)
            {
                DimensionType newType = GetDimensionType(meta.Dimensions[i]);
                if (newType == DimensionType.Ordinal || newType == DimensionType.Nominal)
                {
                    meta.Dimensions[i] = new(
                        meta.Dimensions[i].Code,
                        meta.Dimensions[i].Name,
                        meta.Dimensions[i].AdditionalProperties,
                        meta.Dimensions[i].Values,
                        newType);
                }
            }
        }

        // TODO: Move this too
        /// <summary>
        /// Assigns a dimension type to the given dimension based on its meta-id property.
        /// </summary>
        /// <param name="dimension"></param>
        /// <returns></returns>
        private static DimensionType GetDimensionType(Dimension dimension)
        {
            // If the dimension already has a defining type, ordinality should not overrun it
            if (dimension.Type == DimensionType.Unknown ||
                dimension.Type == DimensionType.Other)
            {
                string propertyKey = PxFileConstants.META_ID;
                if (dimension.AdditionalProperties.TryGetValue(propertyKey, out MetaProperty? prop) &&
                    prop is MultilanguageStringProperty mlsProp)
                {
                    dimension.AdditionalProperties.Remove(propertyKey); // OBS: Remove the property after retrieval
                    if (mlsProp.Value.UniformValue().Equals(PxFileConstants.ORDINAL_VALUE)) return DimensionType.Ordinal;
                    else if (mlsProp.Value.UniformValue().Equals(PxFileConstants.NOMINAL_VALUE)) return DimensionType.Nominal;
                }
            }
            return dimension.Type;
        }

    }
}
