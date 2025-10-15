using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Data;
using Px.Utils.Models.Metadata.Dimensions;
using Px.Utils.Models.Metadata.Enums;
using Px.Utils.Models.Metadata.ExtensionMethods;
using Px.Utils.Models.Metadata;
using PxApi.Models.JsonStat;
using PxApi.Utilities;
using System.Globalization;

namespace PxApi.ModelBuilders
{
    /// <summary>
    /// Collection of static methods for building the response metadata models from the metadata provided by Px.Utils.
    /// </summary>
    public static class JsonStat2Builder
    {
        /// <summary>
        /// Builds a JSON-stat 2.0 format response from matrix metadata and data values.
        /// </summary>
        /// <param name="meta">Input <see cref="IReadOnlyMatrixMetadata"/> containing the structure and metadata</param>
        /// <param name="data">The <see cref="DoubleDataValue"/> array containing the actual data values</param>
        /// <param name="lang">Language of the response, if not provided the default language will be used</param>
        /// <returns><see cref="JsonStat2"/> object representing the data in JSON-stat 2.0 format</returns>
        public static JsonStat2 BuildJsonStat2(IReadOnlyMatrixMetadata meta, DoubleDataValue[] data, string? lang = null)
        {
            JsonStat2 jsonStat2Meta = BuildJsonStat2(meta, lang);
            jsonStat2Meta.Value = data;
            jsonStat2Meta.Status = BuildStatusDictionary(data);
            return jsonStat2Meta;
        }

        /// <summary>
        /// Builds a JSON-stat 2.0 format response from matrix metadata.
        /// </summary>
        /// <param name="meta">Input <see cref="IReadOnlyMatrixMetadata"/> containing the structure and metadata</param>
        /// <param name="lang">Language of the response, if not provided the default language will be used</param>
        /// <returns><see cref="JsonStat2"/> object representing the data in JSON-stat 2.0 format</returns>
        public static JsonStat2 BuildJsonStat2(IReadOnlyMatrixMetadata meta, string? lang = null)
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
                Value = [],
                Role = BuildJsonStatRoles(meta),
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

        private static string GetSourceByLang(IReadOnlyMatrixMetadata meta, string lang)
        {
            return meta.GetContentDimension().AdditionalProperties.GetValueByLanguage(PxFileConstants.SOURCE, lang)
                ?? meta.AdditionalProperties.GetValueByLanguage(PxFileConstants.SOURCE, lang) 
                ?? "";
        }
    }
}
