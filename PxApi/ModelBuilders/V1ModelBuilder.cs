using Px.Utils.Models.Metadata;
using Px.Utils.Models.Metadata.Dimensions;
using Px.Utils.Models.Metadata.Enums;
using Px.Utils.Models.Metadata.ExtensionMethods;
using Px.Utils.Models.Metadata.MetaProperties;
using PxApi.Models.V1;

namespace PxApi.ModelBuilders
{
    public static class V1ModelBuilder
    {
        public static TableV1 BuildTableV1(IReadOnlyMatrixMetadata meta, string urlRoot, string? lang = null)
        {
            lang ??= meta.DefaultLanguage;

            List<DimensionV1> dimensions = meta.Dimensions
                .Where(d => d.Type is not DimensionType.Time or DimensionType.Content)
                .Select(d => BuildDimensionV1(d, urlRoot, lang))
                .ToList();

            return new TableV1()
            {
                Contents = GetValueByLanguage(meta.AdditionalProperties, "CONTENTS", lang),
                Description = GetValueByLanguage(meta.AdditionalProperties, "DESCRIPTION", lang),
                Note = GetValueByLanguage(meta.AdditionalProperties, "NOTE", lang),
                ContentDimension = BuildContentDimensionV1(meta, urlRoot, lang),
                TimeDimension = BuildTimeDimensionV1(meta.GetTimeDimension(), urlRoot, lang),
                ClassificatoryDimensions = dimensions,
                FirstPeriod = meta.GetTimeDimension().Values[0].Name[lang],
                LastPeriod = meta.GetTimeDimension().Values[^1].Name[lang],
                ID = GetValueByLanguage(meta.AdditionalProperties, "TABLEID", lang),
                LastModified = meta.GetContentDimension().Values.Map(v => v.LastUpdated).Max()
            };
        }

        public static ContentDimensionV1 BuildContentDimensionV1(IReadOnlyMatrixMetadata meta, string urlBase, string lang)
        {
            ContentDimension contentDim = meta.GetContentDimension();
            string? tableOrDimSource = GetSourceByLang(meta, lang);

            List<ContentDimensionValueV1> values = contentDim.Values
                .Map(v =>
                {
                    string? source = GetValueByLanguage(v.AdditionalProperties, "SOURCE", lang) ?? tableOrDimSource;
                    return BuildContentDimensionValueV1(v, source, lang);
                })
                .ToList();

            return new ContentDimensionV1()
            {
                Code = contentDim.Code,
                Name = contentDim.Name[lang],
                Note = GetValueByLanguage(contentDim.AdditionalProperties, "NOTE", lang),
                Values = values,
                Url = $"{urlBase}/{contentDim.Code}?lang={lang}"
            };
        }

        public static TimeDimensionV1 BuildTimeDimensionV1(TimeDimension meta, string urlBase, string lang)
        {
            return new TimeDimensionV1()
            {
                Code = meta.Code,
                Name = meta.Name[lang],
                Note = GetValueByLanguage(meta.AdditionalProperties, "NOTE", lang),
                Interval = meta.Interval,
                Size = meta.Values.Count,
                Url = $"{urlBase}/{meta.Code}?lang={lang}",
                Values = meta.Values.Select(v => BuildDimensionValueV1(v, lang)).ToList()
            };
        }

        public static DimensionV1 BuildDimensionV1(IReadOnlyDimension meta, string urlBase, string lang)
        {
            return new DimensionV1()
            {
                Code = meta.Code,
                Name = meta.Name[lang],
                Note = GetValueByLanguage(meta.AdditionalProperties, "NOTE", lang),
                Size = meta.Values.Count,
                Type = meta.Type,
                Url = $"{urlBase}/{meta.Code}?lang={lang}",
                Values =  meta.Values.Select(v => BuildDimensionValueV1(v, lang)).ToList()
            };
        }

        public static DimensionValueV1 BuildDimensionValueV1(IReadOnlyDimensionValue meta, string lang)
        {
            return new DimensionValueV1()
            {
                Code = meta.Code,
                Name = meta.Name[lang],
                Note = GetValueByLanguage(meta.AdditionalProperties, "NOTE", lang),
            };
        }

        public static ContentDimensionValueV1 BuildContentDimensionValueV1(ContentDimensionValue dimMeta, string source, string lang)
        {
            return new ContentDimensionValueV1()
            {
                Code = dimMeta.Code,
                Name = dimMeta.Name[lang],
                Note = GetValueByLanguage(dimMeta.AdditionalProperties, "NOTE", lang),
                LastUpdated = dimMeta.LastUpdated,
                Unit = dimMeta.Unit[lang],
                Precision = dimMeta.Precision,
                Source = source
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

        private static string GetSourceByLang(IReadOnlyMatrixMetadata meta, string lang)
        {
            return GetValueByLanguage(meta.AdditionalProperties, "SOURCE", lang)
                ?? GetValueByLanguage(meta.GetContentDimension().AdditionalProperties, "SOURCE", lang)
                ?? "";
        }
    }
}
