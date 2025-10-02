using PxApi.Models.QueryFilters;

namespace PxApi.Utilities
{
    /// <summary>
    /// Provides utility methods for working with query filters and URL parameters.
    /// </summary>
    public static class QueryFilterUtils
    {
        /// <summary>
        /// Converts an array of filter specifications to a dictionary of Filter objects.
        /// 
        /// Each filter specification must follow the format 'dimension:filterType=value':
        /// - dimension:code=value1,value2,value3 - Creates a CodeFilter with multiple values
        /// - dimension:code=* - Creates a CodeFilter that matches all values (wildcard)
        /// - dimension:from=valueX - Creates a FromFilter starting from valueX
        /// - dimension:to=valueX - Creates a ToFilter up to valueX
        /// - dimension:first=N - Creates a FirstFilter that takes the first N values
        /// - dimension:last=N - Creates a LastFilter that takes the last N values
        /// 
        /// For example:
        /// ["gender:code=1,2", "year:from=2020", "region:last=5"]
        /// 
        /// This will create filters for gender codes "1" and "2", years from 2020 onwards, 
        /// and the last 5 regions.
        /// </summary>
        /// <param name="filtersArray">Array of filter specifications</param>
        /// <returns>Dictionary of dimension codes and corresponding Filter objects</returns>
        public static Dictionary<string, Filter> ConvertFiltersArrayToFilters(string[] filtersArray)
        {
            Dictionary<string, Filter> filters = [];
            
            if (filtersArray.Length == 0)
            {
                return filters;
            }

            foreach (string filterSpec in filtersArray)
            {
                if (string.IsNullOrWhiteSpace(filterSpec))
                {
                    continue;
                }

                // Each spec should be: dimension:filterType=value
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

                // Check for duplicate dimensions
                if (filters.ContainsKey(dimensionCode))
                {
                    throw new ArgumentException($"Duplicate dimension '{dimensionCode}' found in filters array.");
                }

                filters[dimensionCode] = CreateFilterFromTypeAndValue(filterType, value, dimensionCode);
            }

            return filters;
        }

        /// <summary>
        /// Creates a Filter object based on the filter type and value.
        /// </summary>
        /// <param name="filterType">The filter type (code, from, to, first, last)</param>
        /// <param name="value">The filter value</param>
        /// <param name="dimensionCode">The dimension code (for error reporting)</param>
        /// <returns>The appropriate Filter object</returns>
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