using Px.Utils.Models.Metadata;
using Px.Utils.Models.Metadata.Dimensions;
using Px.Utils.Models.Metadata.Enums;
using Px.Utils.Models.Metadata.ExtensionMethods;
using Px.Utils.Models.Metadata.MetaProperties;
using PxApi.Models;
using PxApi.Utilities;

namespace PxApi.ModelBuilders
{
    public static class ModelBuilder
    {
        public static TableMeta BuildTableMeta(IReadOnlyMatrixMetadata meta, Uri urlRoot, string? lang = null)
        {
            lang ??= meta.DefaultLanguage;

            List<Variable> dimensions = meta.Dimensions
                .Where(d => d.Type is not DimensionType.Time or DimensionType.Content)
                .Select(d => BuildVariable(d, urlRoot, lang))
                .ToList();

            return new TableMeta()
            {
                Contents = GetValueByLanguage(meta.AdditionalProperties, PxFileConstants.CONTENTS, lang),
                Description = GetValueByLanguage(meta.AdditionalProperties, PxFileConstants.DESCRIPTION, lang),
                Note = GetValueByLanguage(meta.AdditionalProperties, PxFileConstants.NOTE, lang),
                ContentVariable = BuildContentVariable(meta, urlRoot, lang),
                TimeVariable = BuildTimeVariable(meta, urlRoot, lang),
                ClassificatoryVariables = dimensions,
                FirstPeriod = meta.GetTimeDimension().Values[0].Name[lang],
                LastPeriod = meta.GetTimeDimension().Values[^1].Name[lang],
                ID = GetValueByLanguage(meta.AdditionalProperties, PxFileConstants.TABLEID, lang),
                LastModified = meta.GetContentDimension().Values.Map(v => v.LastUpdated).Max()
            };
        }

        public static ContentVariable BuildContentVariable(IReadOnlyMatrixMetadata meta, Uri urlBase, string lang)
        {
            ContentDimension contentDim = meta.GetContentDimension();
            string? tableOrDimSource = GetSourceByLang(meta, lang);

            List<ContentValue> values = contentDim.Values
                .Map(v =>
                {
                    string? source = GetValueByLanguage(v.AdditionalProperties, PxFileConstants.SOURCE, lang) ?? tableOrDimSource;
                    return BuildContentValue(v, source, lang);
                })
                .ToList();

            return new ContentVariable()
            {
                Code = contentDim.Code,
                Name = contentDim.Name[lang],
                Note = GetValueByLanguage(contentDim.AdditionalProperties, PxFileConstants.NOTE, lang),
                Values = values,
                Url = urlBase.AddRelativePath(contentDim.Code).AddQueryParameters(("lang", lang))
            };
        }

        public static TimeVariable BuildTimeVariable(IReadOnlyMatrixMetadata meta, Uri urlBase, string lang)
        {
            TimeDimension timeDim = meta.GetTimeDimension();

            return new TimeVariable()
            {
                Code = timeDim.Code,
                Name = timeDim.Name[lang],
                Note = GetValueByLanguage(timeDim.AdditionalProperties, PxFileConstants.NOTE, lang),
                Interval = timeDim.Interval,
                Size = timeDim.Values.Count,
                Url = urlBase.AddRelativePath(timeDim.Code).AddQueryParameters(("lang", lang)),
                Values = timeDim.Values.Select(v => BuildValue(v, lang)).ToList()
            };
        }

        public static Variable BuildVariable(IReadOnlyDimension meta, Uri urlBase, string lang)
        {
            return new Variable()
            {
                Code = meta.Code,
                Name = meta.Name[lang],
                Note = GetValueByLanguage(meta.AdditionalProperties, PxFileConstants.NOTE, lang),
                Size = meta.Values.Count,
                Type = meta.Type,
                Url = urlBase.AddRelativePath(meta.Code).AddQueryParameters(("lang", lang)),
                Values =  meta.Values.Select(v => BuildValue(v, lang)).ToList()
            };
        }

        public static Value BuildValue(IReadOnlyDimensionValue meta, string lang)
        {
            return new Value()
            {
                Code = meta.Code,
                Name = meta.Name[lang],
                Note = GetValueByLanguage(meta.AdditionalProperties, PxFileConstants.NOTE, lang),
            };
        }

        public static ContentValue BuildContentValue(ContentDimensionValue dimValMeta, string source, string lang)
        {
            return new ContentValue()
            {
                Code = dimValMeta.Code,
                Name = dimValMeta.Name[lang],
                Note = GetValueByLanguage(dimValMeta.AdditionalProperties, PxFileConstants.NOTE, lang),
                LastUpdated = dimValMeta.LastUpdated,
                Unit = dimValMeta.Unit[lang],
                Precision = dimValMeta.Precision,
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
            return GetValueByLanguage(meta.AdditionalProperties, PxFileConstants.SOURCE, lang)
                ?? GetValueByLanguage(meta.GetContentDimension().AdditionalProperties, PxFileConstants.SOURCE, lang)
                ?? "";
        }
    }
}
