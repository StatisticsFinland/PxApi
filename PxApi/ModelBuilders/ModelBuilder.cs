using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Data;
using Px.Utils.Models.Metadata.Dimensions;
using Px.Utils.Models.Metadata.Enums;
using Px.Utils.Models.Metadata.ExtensionMethods;
using Px.Utils.Models.Metadata.MetaProperties;
using Px.Utils.Models.Metadata;
using PxApi.Models.JsonStat;
using PxApi.Models;
using PxApi.Utilities;
using System.Globalization;

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
        /// <param name="baseUrlWithParams">Url used to costruct the <see cref="Link"/> objests in the response.</param>
        /// <param name="lang">Language of the response, if not provided the default language of the input <paramref name="meta"/> will be used.</param>
        /// <param name="showValues">If true the dimension values will be included. If not provided, defaults to false.</param>
        /// <returns><see cref="TableMeta"/> based on the input meta.</returns>
        public static TableMeta BuildTableMeta(IReadOnlyMatrixMetadata meta, Uri baseUrlWithParams, string? lang = null, bool? showValues = null)
        {
            lang ??= meta.DefaultLanguage;
            bool includeValues = showValues ?? false;
            const string rel = "describedby";

            List<Models.ClassificatoryDimension> dimensions = [.. meta.Dimensions
                .Where(d => d.Type is not DimensionType.Time and not DimensionType.Content)
                .Select(d => BuildDimension(d, lang, includeValues, baseUrlWithParams, rel))];

            string? subjectCode = meta.AdditionalProperties.GetValueByLanguage(PxFileConstants.SUBJECT_CODE, lang);

            return new TableMeta()
            {
                Contents = meta.AdditionalProperties.GetValueByLanguage(PxFileConstants.CONTENTS, lang),
                Title = meta.AdditionalProperties.GetValueByLanguage(PxFileConstants.DESCRIPTION, lang)
                    ?? throw new ArgumentException($"No {PxFileConstants.DESCRIPTION} found in table level metadata."),
                Note = meta.AdditionalProperties.GetValueByLanguage(PxFileConstants.NOTE, lang),
                ContentDimension = BuildContentDimension(meta, lang, includeValues, baseUrlWithParams, rel),
                TimeDimension = BuildTimeDimension(meta, lang, includeValues, baseUrlWithParams, rel),
                ClassificatoryDimensions = dimensions,
                FirstPeriod = meta.GetTimeDimension().Values[0].Name[lang],
                LastPeriod = meta.GetTimeDimension().Values[^1].Name[lang],
                ID = meta.AdditionalProperties.GetValueByLanguage(PxFileConstants.TABLEID, lang)
                    ?? throw new ArgumentException($"No {PxFileConstants.TABLEID} found in table level metadata."),
                LastModified = meta.GetContentDimension().Values.Map(v => v.LastUpdated).Max(),
                Groupings = [],
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
        /// Builds a JSON-stat 2.0 format response from matrix metadata and data values.
        /// </summary>
        /// <param name="meta">Input <see cref="IReadOnlyMatrixMetadata"/> containing the structure and metadata</param>
        /// <param name="data">The <see cref="DoubleDataValue"/> array containing the actual data values</param>
        /// <param name="lang">Language of the response, if not provided the default language will be used</param>
        /// <returns><see cref="JsonStat2"/> object representing the data in JSON-stat 2.0 format</returns>
        public static JsonStat2 BuildJsonStat2(IReadOnlyMatrixMetadata meta, DoubleDataValue[] data, string? lang = null)
        {
            // Use default language if none specified
            string actualLang = lang ?? meta.DefaultLanguage;
            
            // Get table ID and title
            string tableId = meta.AdditionalProperties.GetValueByLanguage(PxFileConstants.TABLEID, actualLang) 
                ?? throw new ArgumentException($"No {PxFileConstants.TABLEID} found in table level metadata.");
            
            string tableLabel = meta.AdditionalProperties.GetValueByLanguage(PxFileConstants.DESCRIPTION, actualLang)
                ?? throw new ArgumentException($"No {PxFileConstants.DESCRIPTION} found in table level metadata.");
            
            DateTime lastUpdated = meta.GetContentDimension().Values.Map(v => v.LastUpdated).Max();
            string updated = lastUpdated.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            
            // Build extension with missing value descriptions
            Dictionary<string, object> extension = [];
            Dictionary<DataValueType, string> translations = PxFileConstants.MISSING_DATA_TRANSLATIONS.GetValueOrDefault(actualLang) 
                ?? throw new ArgumentException($"No missing data translations found for language '{actualLang}'");
                extension["MissingValueDescriptions"] = translations;
            
            return new JsonStat2
            {
                Id = tableId,
                Label = tableLabel,
                Source = GetSourceByLang(meta, actualLang),
                Updated = updated,
                Dimension = BuildJsonStatDimensions(meta, actualLang),
                Size = [.. meta.Dimensions.Select(d => d.Values.Count)],
                Value = data,
                Role = BuildJsonStatRoles(meta),
                Status = BuildStatusDictionary(data),
                Extension = extension
            };
        }

        /// <summary>
        /// Builds a status dictionary for the JSON-stat format that maps indices of missing data
        /// values to status codes that explain why they're missing.
        /// </summary>
        /// <param name="data">The array of data values to process</param>
        /// <returns>A dictionary where keys are indices of missing values and values are status codes,
        /// or null if there are no missing values</returns>
        public static Dictionary<int, string>? BuildStatusDictionary(DoubleDataValue[] data)
        {
            Dictionary<int, string> status = [];
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i].Type != DataValueType.Exists)
                {
                    status.Add(i, ((byte)data[i].Type).ToString());
                }
            }
            
            // If no missing values were found, return null
            return status.Count > 0 ? status : null;
        }

        private static Dictionary<string, Models.JsonStat.Dimension> BuildJsonStatDimensions(IReadOnlyMatrixMetadata meta, string lang)
        {
            Dictionary<string, Models.JsonStat.Dimension> dimensions = [];
            
            foreach (IReadOnlyDimension dimension in meta.Dimensions)
            {
                Models.JsonStat.Dimension jsonDimension = new()
                {
                    Label = dimension.Name[lang],
                    Category = new Category
                    {
                        Index = [],
                        Label = []
                    }
                };
                
                foreach (IReadOnlyDimensionValue value in dimension.Values)
                {
                    jsonDimension.Category.Index?.Add(value.Code);
                    jsonDimension.Category.Label[value.Code] = value.Name[lang];
                }
                
                // Add unit information for content dimensions
                if (dimension.Type == DimensionType.Content)
                {
                    Px.Utils.Models.Metadata.Dimensions.ContentDimension contentDim = (Px.Utils.Models.Metadata.Dimensions.ContentDimension)dimension;
                    jsonDimension.Category.Unit = [];
                    
                    foreach (ContentDimensionValue value in contentDim.Values)
                    {
                        jsonDimension.Category.Unit[value.Code] = new Unit
                        {
                            Label = value.Unit[lang],
                            Decimals = value.Precision
                        };
                    }
                }
                
                dimensions[dimension.Code] = jsonDimension;
            }
            
            return dimensions;
        }

        /// <summary>
        /// Builds the roles dictionary for the JSON-stat format
        /// </summary>
        /// <param name="meta">The matrix metadata</param>
        /// <returns>Dictionary of role objects</returns>
        private static Dictionary<string, List<string>> BuildJsonStatRoles(IReadOnlyMatrixMetadata meta)
        {
            Dictionary<string, List<string>> roles = new()
            {
                { "time", [meta.GetTimeDimension().Code] },
                { "metric", [meta.GetContentDimension().Code] }
            };
            
            // Add geo role if geographical dimensions exist
            // This is a placeholder - in a real implementation you'd need logic to identify geographical dimensions
            var geoDimensions = meta.Dimensions.Where(d => d.Type == DimensionType.Geographical).Select(d => d.Code).ToList();
            if (geoDimensions.Count > 0)
            {
                roles["geo"] = geoDimensions;
            }
            
            return roles;
        }

        /// <summary>
        /// Build a <see cref="ContentDimension"/> object based on the input <paramref name="meta"/>.
        /// </summary>
        /// <param name="meta">Input <see cref="IReadOnlyMatrixMetadata"/></param>
        /// <param name="lang">Language of the response, language must be found in the provided <paramref name="meta"/>.</param>
        /// <param name="showValues">If true the dimension values will be included.</param>
        /// <param name="baseUrlWithParams">Url used to costruct the <see cref="Link"/> objests in the response.</param>
        /// <param name="rel">The relation type in the links pointing to this object.</param>
        /// <returns><see cref="ContentDimension"/> based on the provided <paramref name="meta"/></returns>
        public static Models.ContentDimension BuildContentDimension(IReadOnlyMatrixMetadata meta, string lang, bool showValues, Uri baseUrlWithParams, string rel)
        {
            Px.Utils.Models.Metadata.Dimensions.ContentDimension contentDim = meta.GetContentDimension();
            string? tableOrDimSource = GetSourceByLang(meta, lang);

            List<ContentValue>? values = showValues
                ? [.. contentDim.Values.Map(v =>
                {
                    string? source = v.AdditionalProperties.GetValueByLanguage(PxFileConstants.SOURCE, lang) ?? tableOrDimSource;
                    return BuildContentValue(v, source, lang);
                })]
                : null;

            return new Models.ContentDimension()
            {
                Code = contentDim.Code,
                Name = contentDim.Name[lang],
                Note = contentDim.AdditionalProperties.GetValueByLanguage(PxFileConstants.NOTE, lang),
                Size = contentDim.Values.Count,
                Values = values,
                Links = BuildDimensionLinks(baseUrlWithParams, contentDim.Code, rel)
            };
        }

        /// <summary>
        /// Build a <see cref="TimeDimension"/> object based on the input <paramref name="meta"/>.
        /// </summary>
        /// <param name="meta">Input <see cref="IReadOnlyMatrixMetadata"/></param>
        /// <param name="lang">Language of the response, language must be found in the provided <paramref name="meta"/>.</param>
        /// <param name="showValues">If true the dimension values will be included.</param>
        /// <param name="baseUrlWithParams">Url used to costruct the <see cref="Link"/> objests in the response.</param>
        /// <param name="rel">The relation type in the links pointing to this object.</param>
        /// <returns><see cref="TimeDimension"/> based on the provided <paramref name="meta"/></returns>
        public static Models.TimeDimension BuildTimeDimension(IReadOnlyMatrixMetadata meta, string lang, bool showValues, Uri baseUrlWithParams, string rel)
        {
            Px.Utils.Models.Metadata.Dimensions.TimeDimension timeDim = meta.GetTimeDimension();

            return new Models.TimeDimension()
            {
                Code = timeDim.Code,
                Name = timeDim.Name[lang],
                Note = timeDim.AdditionalProperties.GetValueByLanguage(PxFileConstants.NOTE, lang),
                Interval = timeDim.Interval,
                Size = timeDim.Values.Count,
                Values = showValues ? [.. timeDim.Values.Select<global::Px.Utils.Models.Metadata.Dimensions.IReadOnlyDimensionValue,global::PxApi.Models.Value>((global::Px.Utils.Models.Metadata.Dimensions.IReadOnlyDimensionValue v) => global::PxApi.ModelBuilders.ModelBuilder.BuildValue(v, lang))] : null,
                Links = BuildDimensionLinks(baseUrlWithParams, timeDim.Code, rel)
            };
        }

        /// <summary>
        /// Build a <see cref="Models.ClassificatoryDimension"/> object based on the input <paramref name="meta"/>.
        /// </summary>
        /// <param name="meta">Input <see cref="IReadOnlyDimension"/></param>
        /// <param name="lang">Language of the response, language must be found in the provided <paramref name="meta"/>.</param>
        /// <param name="showValues">If true the dimension values will be included.</param>
        /// <param name="baseUrlWithParams">Url used to costruct the <see cref="Link"/> objests in the response.</param>
        /// <param name="rel">The relation type in the links pointing to this object.</param>
        /// <returns><see cref="Models.ClassificatoryDimension"/> based on the provided <paramref name="meta"/></returns>
        public static Models.ClassificatoryDimension BuildDimension(IReadOnlyDimension meta, string lang, bool showValues, Uri baseUrlWithParams, string rel)
        {
            return new Models.ClassificatoryDimension()
            {
                Code = meta.Code,
                Name = meta.Name[lang],
                Note = meta.AdditionalProperties.GetValueByLanguage(PxFileConstants.NOTE, lang),
                Size = meta.Values.Count,
                Type = meta.Type,
                Values = showValues ? [.. meta.Values.Select<global::Px.Utils.Models.Metadata.Dimensions.IReadOnlyDimensionValue,global::PxApi.Models.Value>((global::Px.Utils.Models.Metadata.Dimensions.IReadOnlyDimensionValue v) => global::PxApi.ModelBuilders.ModelBuilder.BuildValue(v, lang))] : null,
                Links = BuildDimensionLinks(baseUrlWithParams, meta.Code, rel)
            };
        }

        /// <summary>
        /// Build a <see cref="Value"/> for a <see cref="Models.ClassificatoryDimension"/> based on the input <paramref name="meta"/>.
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
        /// Build a <see cref="ContentValue"/> for a <see cref="ContentDimension"/> based on the input <paramref name="meta"/>.
        /// </summary>
        /// <param name="meta">Input <see cref="IReadOnlyDimensionValue"/></param>
        /// <param name="source">The source information for the content value. This is not necessarily in the <paramref name="meta"/>.</param>
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

        private static List<Link> BuildDimensionLinks(Uri urlBaseWithParams, string dimensionCode, string rel)
        {
            return [
                new Link()
                {
                    Rel = rel,
                    Href = urlBaseWithParams
                    .AddRelativePath(dimensionCode)
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
            return meta.GetContentDimension().AdditionalProperties.GetValueByLanguage(PxFileConstants.SOURCE, lang)
                ?? meta.AdditionalProperties.GetValueByLanguage(PxFileConstants.SOURCE, lang) 
                ?? "";
        }
    }
}
