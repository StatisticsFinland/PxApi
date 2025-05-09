﻿using Px.Utils.Models.Metadata;
using Px.Utils.Models.Metadata.Dimensions;
using Px.Utils.Models.Metadata.Enums;
using Px.Utils.Models.Metadata.ExtensionMethods;
using Px.Utils.Models.Metadata.MetaProperties;
using PxApi.Models;
using PxApi.Utilities;

namespace PxApi.ModelBuilders
{
    /// <summary>
    /// Collection of static methods for building the response metadata models from the metadata provided by Px.Utils.
    /// </summary>
    public static class ModelBuilder
    {
        /// <summary>
        /// Build the <see cref="TableMeta"/> objects.
        /// </summary>
        /// <param name="meta">Input <see cref="IReadOnlyMatrixMetadata"/></param>
        /// <param name="groups">List of groups that the table is part of.</param>
        /// <param name="baseUrlWithParams">Url used to costruct the <see cref="Link"/> objests in the response.</param>
        /// <param name="lang">Language of the response, if not provided the default language of the input <paramref name="meta"/> will be used.</param>
        /// <param name="showValues">If true the variable values will be included. If not provided, defaults to false.</param>
        /// <returns><see cref="TableMeta"/> based on the input meta.</returns>
        public static TableMeta BuildTableMeta(IReadOnlyMatrixMetadata meta, List<TableGroup> groups, Uri baseUrlWithParams, string? lang = null, bool? showValues = null)
        {
            lang ??= meta.DefaultLanguage;
            bool includeValues = showValues ?? false;
            const string rel = "describedby";

            List<Variable> dimensions = [.. meta.Dimensions
                .Where(d => d.Type is not DimensionType.Time and not DimensionType.Content)
                .Select(d => BuildVariable(d, lang, includeValues, baseUrlWithParams, rel))];

            return new TableMeta()
            {
                Contents = meta.AdditionalProperties.GetValueByLanguage(PxFileConstants.CONTENTS, lang),
                Title = meta.AdditionalProperties.GetValueByLanguage(PxFileConstants.DESCRIPTION, lang)
                    ?? throw new ArgumentException($"No {PxFileConstants.DESCRIPTION} found in table level metadata."),
                Note = meta.AdditionalProperties.GetValueByLanguage(PxFileConstants.NOTE, lang),
                ContentVariable = BuildContentVariable(meta, lang, includeValues, baseUrlWithParams, rel),
                TimeVariable = BuildTimeVariable(meta, lang, includeValues, baseUrlWithParams, rel),
                ClassificatoryVariables = dimensions,
                FirstPeriod = meta.GetTimeDimension().Values[0].Name[lang],
                LastPeriod = meta.GetTimeDimension().Values[^1].Name[lang],
                ID = meta.AdditionalProperties.GetValueByLanguage(PxFileConstants.TABLEID, lang)
                    ?? throw new ArgumentException($"No {PxFileConstants.TABLEID} found in table level metadata."),
                LastModified = meta.GetContentDimension().Values.Map(v => v.LastUpdated).Max(),
                Groupings = groups,
                Links =
                [
                    new Link()
                    {
                        Rel = "self",
                        Href = baseUrlWithParams.ToString(),
                        Method = "GET"
                    }
                ]
            };
        }

        /// <summary>
        /// Build a <see cref="ContentVariable"/> object based on the input <paramref name="meta"/>.
        /// </summary>
        /// <param name="meta">Input <see cref="IReadOnlyMatrixMetadata"/></param>
        /// <param name="lang">Language of the response, language must be found in the provided <paramref name="meta"/>.</param>
        /// <param name="showValues">If true the variable values will be included.</param>
        /// <param name="baseUrlWithParams">Url used to costruct the <see cref="Link"/> objests in the response.</param>
        /// <param name="rel">The relation type in the links pointing to this object.</param>
        /// <returns><see cref="ContentVariable"/> based on the provided <paramref name="meta"/></returns>
        public static ContentVariable BuildContentVariable(IReadOnlyMatrixMetadata meta, string lang, bool showValues, Uri baseUrlWithParams, string rel)
        {
            ContentDimension contentDim = meta.GetContentDimension();
            string? tableOrDimSource = GetSourceByLang(meta, lang);

            List<ContentValue>? values = showValues
                ? [.. contentDim.Values.Map(v =>
                {
                    string? source = v.AdditionalProperties.GetValueByLanguage(PxFileConstants.SOURCE, lang) ?? tableOrDimSource;
                    return BuildContentValue(v, source, lang);
                })]
                : null;

            return new ContentVariable()
            {
                Code = contentDim.Code,
                Name = contentDim.Name[lang],
                Note = contentDim.AdditionalProperties.GetValueByLanguage(PxFileConstants.NOTE, lang),
                Size = contentDim.Values.Count,
                Values = values,
                Links = BuildVariableLinks(baseUrlWithParams, contentDim.Code, rel)
            };
        }

        /// <summary>
        /// Build a <see cref="TimeVariable"/> object based on the input <paramref name="meta"/>.
        /// </summary>
        /// <param name="meta">Input <see cref="IReadOnlyMatrixMetadata"/></param>
        /// <param name="lang">Language of the response, language must be found in the provided <paramref name="meta"/>.</param>
        /// <param name="showValues">If true the variable values will be included.</param>
        /// <param name="baseUrlWithParams">Url used to costruct the <see cref="Link"/> objests in the response.</param>
        /// <param name="rel">The relation type in the links pointing to this object.</param>
        /// <returns><see cref="TimeVariable"/> based on the provided <paramref name="meta"/></returns>
        public static TimeVariable BuildTimeVariable(IReadOnlyMatrixMetadata meta, string lang, bool showValues, Uri baseUrlWithParams, string rel)
        {
            TimeDimension timeDim = meta.GetTimeDimension();

            return new TimeVariable()
            {
                Code = timeDim.Code,
                Name = timeDim.Name[lang],
                Note = timeDim.AdditionalProperties.GetValueByLanguage(PxFileConstants.NOTE, lang),
                Interval = timeDim.Interval,
                Size = timeDim.Values.Count,
                Values = showValues ? [.. timeDim.Values.Select(v => BuildValue(v, lang))] : null,
                Links = BuildVariableLinks(baseUrlWithParams, timeDim.Code, rel)
            };
        }

        /// <summary>
        /// Build a <see cref="Variable"/> object based on the input <paramref name="meta"/>.
        /// </summary>
        /// <param name="meta">Input <see cref="IReadOnlyDimension"/></param>
        /// <param name="lang">Language of the response, language must be found in the provided <paramref name="meta"/>.</param>
        /// <param name="showValues">If true the variable values will be included.</param>
        /// <param name="baseUrlWithParams">Url used to costruct the <see cref="Link"/> objests in the response.</param>
        /// <param name="rel">The relation type in the links pointing to this object.</param>
        /// <returns><see cref="Variable"/> based on the provided <paramref name="meta"/></returns>
        public static Variable BuildVariable(IReadOnlyDimension meta, string lang, bool showValues, Uri baseUrlWithParams, string rel)
        {
            return new Variable()
            {
                Code = meta.Code,
                Name = meta.Name[lang],
                Note = meta.AdditionalProperties.GetValueByLanguage(PxFileConstants.NOTE, lang),
                Size = meta.Values.Count,
                Type = meta.Type,
                Values = showValues ? [.. meta.Values.Select(v => BuildValue(v, lang))] : null,
                Links = BuildVariableLinks(baseUrlWithParams, meta.Code, rel)
            };
        }

        /// <summary>
        /// Build a <see cref="Value"/> for a <see cref="Variable"/> based on the input <paramref name="meta"/>.
        /// </summary>
        /// <param name="meta">Input <see cref="IReadOnlyDimensionValue"/></param>
        /// <param name="lang">Language of the response, language must be found in the provided <paramref name="meta"/>.</param>
        /// <returns><see cref="Value"/> based on the provided <paramref name="meta"/></returns>
        public static Value BuildValue(IReadOnlyDimensionValue meta, string lang)
        {
            return new Value()
            {
                Code = meta.Code,
                Name = meta.Name[lang],
                Note = GetValueNoteByLanguage(meta.AdditionalProperties, lang),
            };
        }

        /// <summary>
        /// Build a <see cref="ContentValue"/> for a <see cref="ContentVariable"/> based on the input <paramref name="meta"/>.
        /// </summary>
        /// <param name="meta">Input <see cref="IReadOnlyDimensionValue"/></param>
        /// <param name="source">The source information for the content value. This is not nessessarily in the <paramref name="meta"/>.</param>
        /// <param name="lang">Language of the response, language must be found in the provided <paramref name="meta"/>.</param>
        /// <returns><see cref="ContentValue"/> based on the provided <paramref name="meta"/></returns>
        public static ContentValue BuildContentValue(ContentDimensionValue meta, string source, string lang)
        {
            return new ContentValue()
            {
                Code = meta.Code,
                Name = meta.Name[lang],
                Note = GetValueNoteByLanguage(meta.AdditionalProperties, lang),
                LastUpdated = meta.LastUpdated,
                Unit = meta.Unit[lang],
                Precision = meta.Precision,
                Source = source
            };
        }

        private static string? GetValueNoteByLanguage(IReadOnlyDictionary<string, MetaProperty> additionalProperties, string lang)
        {
            return additionalProperties.GetValueByLanguage(PxFileConstants.VALUENOTE, lang)
                ?? additionalProperties.GetValueByLanguage(PxFileConstants.NOTE, lang);
        }

        private static List<Link> BuildVariableLinks(Uri urlBaseWithParams, string variableCode, string rel)
        {
            return [
                new Link()
                {
                    Rel = rel,
                    Href = urlBaseWithParams
                    .AddRelativePath(variableCode)
                    .DropQueryParameters("showValues")
                    .ToString(),
                    Method = "GET"
                },
                new Link()
                {
                    Rel = "up",
                    Href = urlBaseWithParams.ToString(),
                    Method = "GET"
                }
            ];
        }

        private static string GetSourceByLang(IReadOnlyMatrixMetadata meta, string lang)
        {
            return meta.AdditionalProperties.GetValueByLanguage(PxFileConstants.SOURCE, lang)
                ?? meta.GetContentDimension().AdditionalProperties.GetValueByLanguage(PxFileConstants.SOURCE, lang)
                ?? "";
        }
    }
}
