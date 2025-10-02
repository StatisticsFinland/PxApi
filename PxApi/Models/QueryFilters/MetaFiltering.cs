using Px.Utils.Language;
using Px.Utils.Models.Metadata.Dimensions;
using Px.Utils.Models.Metadata.Enums;
using Px.Utils.Models.Metadata.MetaProperties;
using Px.Utils.Models.Metadata;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace PxApi.Models.QueryFilters
{
    /// <summary>
    /// Contains methods for filtering metadata dimensions
    /// </summary>
    public static class MetaFiltering
    {
        /// <summary>
        /// Applies filters to the metadata dimensions and returns a new <see cref="MatrixMap"/> with the filtered dimensions.
        /// If a filter is not provided for a dimension, it applies default filtering based on the dimension type.
        /// </summary>
        /// <param name="meta">The matrix metadata to filter.</param>
        /// <param name="filters">A dictionary of filters to apply to the dimensions.</param>
        /// <returns>A new <see cref="MatrixMap"/> with the filtered dimensions.</returns>
        public static MatrixMap ApplyToMatrixMeta(IReadOnlyMatrixMetadata meta, Dictionary<string, Filter> filters)
        {
            return new([.. meta.Dimensions.Select(dim =>
            {
                if (filters.TryGetValue(dim.Code, out Filter? filter))
                {
                    return filter.Apply(dim);
                }
                else
                {
                    return DefaultFiltering(dim);
                }
            })]);
        }

        /// <summary>
        /// Applies default filtering for all dimensions in metadata dimensions and returns a string to be used as query url parameters
        /// </summary>
        /// <param name="meta">The matrix metadata to filter</param>
        /// <returns>String that represents url parameters to be used for a default query</returns>
        public static string GetDefaultFilteringUrlParameters(IReadOnlyMatrixMetadata meta)
        {
            StringBuilder sb = new("?");
            for (int i = 0; i < meta.Dimensions.Count; i++)
            {
                IReadOnlyDimension dimension = meta.Dimensions[i];
                if (i > 0) sb.Append('&');
                
                (Filter filter, string value) = SelectFilterForDimension(dimension);
                sb.Append($"filters={dimension.Code}:{filter.ParamName}={value}");
            }
            return sb.ToString();
        }

        private static (Filter, string) SelectFilterForDimension(IReadOnlyDimension dimension)
        {
            if (TryGetDefaultValueCode(dimension, out string? code))
            {
                return (new CodeFilter([code]), code);
            }

            if (dimension.Type == DimensionType.Time)
            {
                return (new CodeFilter(["*"]), "*");
            }

            return (new FirstFilter(1), "1");
        }

        private static DimensionMap DefaultFiltering(IReadOnlyDimension dimension)
        {
            Filter filter = SelectFilterForDimension(dimension).Item1;
            return filter.Apply(dimension);
        }

        private static bool TryGetDefaultValueCode(IReadOnlyDimension dim, [NotNullWhen(true)] out string? code)
        {
            if (dim.AdditionalProperties.TryGetValue("ELIMINATION", out MetaProperty? prop))
            {
                if (prop.Type == MetaPropertyType.Text)
                {
                    string valCode = ((StringProperty)prop).Value;
                    code = dim.Values.FirstOrDefault(v => v.Code.Equals(valCode))?.Code;
                }
                else if (prop.Type == MetaPropertyType.MultilanguageText)
                {
                    MultilanguageString valName = ((MultilanguageStringProperty)prop).Value;
                    code = dim.Values.FirstOrDefault(v => v.Name.Equals(valName))?.Code;
                }
                else code = null;
            }
            else code = null;

            return code is not null;
        }
    }
}
