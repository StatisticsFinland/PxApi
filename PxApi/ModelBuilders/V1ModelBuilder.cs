using Px.Utils.Models.Metadata;
using Px.Utils.Models.Metadata.Dimensions;
using Px.Utils.Models.Metadata.ExtensionMethods;
using Px.Utils.Models.Metadata.MetaProperties;
using PxApi.Models.V1;

namespace PxApi.ModelBuilders
{
    public static class V1ModelBuilder
    {
        public static TableV1 BuildTableV1(IReadOnlyMatrixMetadata meta, string lang)
        {
            List<DimensionV1> dimensions = meta.Dimensions
                .Select(d => BuildDimensionV1(d, lang))
                .ToList();

            return new TableV1()
            {
                Contents = GetValueByLanguage(meta.AdditionalProperties, "CONTENTS", lang),
                Description = GetValueByLanguage(meta.AdditionalProperties, "DESCRIPTION", lang),
                Note = GetValueByLanguage(meta.AdditionalProperties, "NOTE", lang),
                Dimensions = dimensions,
                FirstPeriod = meta.GetTimeDimension().Values[0].Name[lang],
                LastPeriod = meta.GetTimeDimension().Values[^1].Name[lang],
                ID = GetValueByLanguage(meta.AdditionalProperties, "TABLEID", lang),
                LastModified = meta.GetContentDimension().Values.Map(v => v.LastUpdated).Max()
            };
        }

        public static DimensionV1 BuildDimensionV1(IReadOnlyDimension meta, string lang)
        {
            return new DimensionV1()
            {
                Code = meta.Code,
                Interval = meta is TimeDimension timeDimension ? timeDimension.Interval : null,
                Name = meta.Name[lang],
                Note = GetValueByLanguage(meta.AdditionalProperties, "NOTE", lang),
                Size = meta.Values.Count,
                Type = meta.Type,
                Url = $"api/v1/meta/{meta.Code}",
                Values =  meta.Values.Select(v => BuildDimensionValueV1(v, lang)).ToList()
            };
        }

        public static DimensionValueV1 BuildDimensionValueV1(IReadOnlyDimensionValue meta, string lang)
        {
            if (meta is ContentDimensionValue contentDimensionValue)
            {
                return BuildContentDimensionValueV1(contentDimensionValue, lang);
            }

            return new DimensionValueV1()
            {
                Code = meta.Code,
                Name = meta.Name[lang],
                Note = GetValueByLanguage(meta.AdditionalProperties, "NOTE", lang),
                LastUpdated = null,
                Unit = null,
                Precision = null,
                Source = null
            };
        }

        public static DimensionValueV1 BuildContentDimensionValueV1(ContentDimensionValue meta, string lang)
        {
            return new DimensionValueV1()
            {
                Code = meta.Code,
                Name = meta.Name[lang],
                Note = GetValueByLanguage(meta.AdditionalProperties, "NOTE", lang),
                LastUpdated = meta.LastUpdated,
                Unit = meta.Unit[lang],
                Precision = meta.Precision,
                Source = GetValueByLanguage(meta.AdditionalProperties, "SOURCE", lang)
            };
        }

        private static string? GetValueByLanguage(IReadOnlyDictionary<string, MetaProperty> propertyCollection, string key, string lang)
        {
            if(propertyCollection.TryGetValue(key, out MetaProperty? property))
            {
                if(property is MultilanguageStringProperty multilanguageStringProperty)
                {
                    return multilanguageStringProperty.Value[lang];
                }
                else if (property is StringProperty stringProperty)
                {
                    return stringProperty.Value;
                }
            }

            return null;
        }
    }
}
