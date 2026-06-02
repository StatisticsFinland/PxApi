using Px.Utils.Models.Metadata;
using Px.Utils.Models.Metadata.Dimensions;
using Px.Utils.Models.Metadata.ExtensionMethods;
using PxApi.ModelBuilders;
using PxApi.Models;

namespace PxApi.Utilities
{
    /// <summary>
    /// Builds <see cref="TableSummary"/> instances from PX metadata.
    /// </summary>
    public static class TableSummaryBuilder
    {
        /// <summary>
        /// Builds a table summary from metadata.
        /// </summary>
        /// <param name="metadata">The metadata used to build the summary.</param>
        /// <param name="tableName">Fallback table identifier when metadata fields are missing.</param>
        /// <param name="lang">Language used for localized metadata values.</param>
        /// <returns>A fully populated <see cref="TableSummary"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown when required summary metadata is missing.</exception>
        public static TableSummary Build(IReadOnlyMatrixMetadata metadata, string tableName, string lang)
        {
            if (!metadata.TryGetContentDimension(out ContentDimension? contentDimension) || contentDimension.Values.Count == 0)
            {
                throw new InvalidOperationException($"Table '{tableName}' does not contain a valid content dimension.");
            }

            string code = metadata.AdditionalProperties.GetValueByLanguage(PxFileConstants.TABLEID, lang) ?? tableName;
            string name = metadata.AdditionalProperties.GetValueByLanguage(PxFileConstants.DESCRIPTION, lang) ?? tableName;
            List<MetricInfo> metrics = contentDimension.Values.Map(value => new MetricInfo
            {
                Name = value.Name[lang],
                Unit = value.Unit[lang]
            }).ToList();

            DateTime lastUpdated = contentDimension.Values.Map(value => value.LastUpdated).Max();
            TimeRange timeRange = BuildTimeRange(metadata, lang);
            List<DimensionInfo> dimensions = metadata.Dimensions
                .Where(dimension => dimension is not ContentDimension && dimension is not TimeDimension)
                .Select(dimension => new DimensionInfo
                {
                    Name = dimension.Name[lang],
                    Size = dimension.Values.Count
                })
                .ToList();

            return new TableSummary
            {
                TableId = code,
                Title = name,
                Metrics = metrics,
                TimeRange = timeRange,
                Dimensions = dimensions,
                LastUpdated = lastUpdated
            };
        }

        private static TimeRange BuildTimeRange(IReadOnlyMatrixMetadata metadata, string lang)
        {
            TimeDimension? timeDimension = metadata.Dimensions.OfType<TimeDimension>().FirstOrDefault();
            if (timeDimension is null || timeDimension.Values.Count == 0)
            {
                return new TimeRange
                {
                    From = string.Empty,
                    To = string.Empty
                };
            }

            List<string> timeNames = timeDimension.Values.Map(value => value.Name[lang]).ToList();
            return new TimeRange
            {
                From = timeNames[0],
                To = timeNames[^1]
            };
        }
    }
}
