using PxApi.Models.QueryFilters;

namespace PxApi.Utilities
{
    /// <summary>
    /// Provides utility methods for parsing and constructing filter objects from query string specifications.
    /// </summary>
    public static class QueryFilterUtils
    {
        /// <summary>
        /// Converts an array of filter specifications to a dictionary of <see cref="Filter"/> instances.
        /// Format per element: 'dimension:filterType=value'. Supported filter types:
        /// code (comma-separated codes, supports '*' wildcard for zero or more characters),
        /// from (inclusive start code or pattern), to (inclusive end code or pattern),
        /// first (positive integer), last (positive integer).
        /// Rules: one filter per dimension, counts must be &gt; 0, wildcards allowed only in code/from/to values.
        /// Throws <see cref="ArgumentException"/> on invalid formats, unsupported types, duplicate dimensions, or invalid numeric counts.
        /// </summary>
        /// <param name="filtersArray">Array of raw filter specification strings.</param>
        /// <returns>Dictionary mapping dimension codes to filter objects.</returns>
        public static Dictionary<string, Filter> ConvertFiltersArrayToFilters(string[] filtersArray)
        {
            Dictionary<string, Filter> filters = [];
            if (filtersArray.Length == 0) return filters;

            foreach (string filterSpec in filtersArray)
            {
                if (string.IsNullOrWhiteSpace(filterSpec)) continue;

                string[] dimensionAndRest = filterSpec.Split(':', 2, StringSplitOptions.TrimEntries);
                if (dimensionAndRest.Length != 2)
                {
                    throw new ArgumentException($"Invalid filter format: '{filterSpec}'. Expected format: dimension:filterType=value");
                }

                string dimensionCode = dimensionAndRest[0];
                string filterTypeAndValue = dimensionAndRest[1];

                string[] typeAndValue = filterTypeAndValue.Split('=', 2, StringSplitOptions.TrimEntries);
                if (typeAndValue.Length != 2)
                {
                    throw new ArgumentException($"Invalid filter format: '{filterSpec}'. Expected format: dimension:filterType=value");
                }

                string filterType = typeAndValue[0].ToLowerInvariant();
                string value = typeAndValue[1];

                if (filters.ContainsKey(dimensionCode))
                {
                    throw new ArgumentException($"Duplicate dimension '{dimensionCode}' found in filters array.");
                }

                filters[dimensionCode] = CreateFilterFromTypeAndValue(filterType, value, dimensionCode);
            }

            return filters;
        }

        /// <summary>
        /// Factory method that creates filter instances based on parsed type/value.
        /// </summary>
        /// <param name="filterType">Lowercase filter type name.</param>
        /// <param name="value">Raw value segment after '='.</param>
        /// <param name="dimensionCode">Dimension code for error context.</param>
        /// <returns>Concrete <see cref="Filter"/> instance.</returns>
        /// <exception cref="ArgumentException">Thrown for unsupported filter type or invalid value.</exception>
        private static Filter CreateFilterFromTypeAndValue(string filterType, string value, string dimensionCode)
        {
            return filterType switch
            {
                CodeFilter.FilterTypeName => new CodeFilter(value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)),
                FromFilter.FilterTypeName => new FromFilter(value),
                ToFilter.FilterTypeName => new ToFilter { FilterString = value },
                FirstFilter.FilterTypeName => int.TryParse(value, out int firstCount) && firstCount > 0
                    ? new FirstFilter(firstCount)
                    : throw new ArgumentException($"Invalid value '{value}' for 'first' filter on dimension '{dimensionCode}'. Must be a positive integer."),
                LastFilter.FilterTypeName => int.TryParse(value, out int lastCount) && lastCount > 0
                    ? new LastFilter(lastCount)
                    : throw new ArgumentException($"Invalid value '{value}' for 'last' filter on dimension '{dimensionCode}'. Must be a positive integer."),
                _ => throw new ArgumentException($"Unsupported filter type '{filterType}' for dimension '{dimensionCode}'")
            };
        }
    }
}