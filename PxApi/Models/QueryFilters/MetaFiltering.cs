using Px.Utils.Language;
using Px.Utils.Models.Metadata.Dimensions;
using Px.Utils.Models.Metadata.Enums;
using Px.Utils.Models.Metadata.MetaProperties;
using Px.Utils.Models.Metadata;
using System.Diagnostics.CodeAnalysis;

namespace PxApi.Models.QueryFilters
{
    public static class MetaFiltering
    {
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

        private static IDimensionMap DefaultFiltering(IReadOnlyDimension dimension)
        {
            if(TryGetDefaultValueCode(dimension, out string? code))
            {
                return new DimensionMap(dimension.Code, [code]);
            }

            if (dimension.Type == DimensionType.Time)
            {
                LastFilter last20 = new(20);
                return last20.Apply(dimension);
            }

            else return dimension;
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
