using PxApi.Models.QueryFilters;

namespace PxApi.Utilities
{
    /// <summary>
    /// Provides utility methods for working with query filters and URL parameters.
    /// </summary>
    public static class QueryFilterUtils
    {
        /// <summary>
        /// Converts URL query parameters to a dictionary of Filter objects.
        /// 
        /// Parameters must follow one of these formats:
        /// 1. dimension:code=value1,value2,value3 - Creates a CodeFilter with multiple values
        /// 2. dimension:code=* - Creates a CodeFilter that matches all values (wildcard)
        /// 3. dimension:from=valueX - Creates a FromFilter starting from valueX
        /// 4. dimension:to=valueX - Creates a ToFilter up to valueX
        /// 5. dimension:first=N - Creates a FirstFilter that takes the first N values
        /// 6. dimension:last=N - Creates a LastFilter that takes the last N values
        /// 
        /// For example:
        /// gender:code=1,2 - Filters the "gender" dimension to codes "1" and "2"
        /// year:from=2020 - Filters the "year" dimension from 2020 and forward
        /// region:code=* - Returns all values for the "region" dimension using a wildcard
        /// year:last=5 - Returns only the last 5 years
        /// </summary>
        /// <param name="queryParameters">Dictionary of URL query parameters</param>
        /// <returns>Dictionary of dimension codes and corresponding Filter objects</returns>
        public static Dictionary<string, IFilter> ConvertUrlParametersToFilters(Dictionary<string, string> queryParameters)
        {
            Dictionary<string, IFilter> filters = [];
            string[] validFilterTypes = ["code", "from", "to", "first", "last"];

            IEnumerable<KeyValuePair<string, string>> filterByDimAndType = queryParameters
                .Where(p => validFilterTypes.Any(ft => p.Key.EndsWith($":{ft}", StringComparison.OrdinalIgnoreCase)));

            // Process each filter type for this dimension
            foreach (KeyValuePair<string, string> param in filterByDimAndType)
            {
                string[] parts = param.Key.Split(':', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2)
                    throw new ArgumentException("Invalid filter parameter format.");

                string dimensionCode = parts[0];
                if (filters.ContainsKey(dimensionCode.ToLower()))
                {
                    throw new ArgumentException("Duplicate dimension code found in query parameters.");
                }
                string filterType = parts[1].ToLower();
                string value = param.Value;

                switch (filterType)
                {
                    case "code":
                        // Handle comma-separated values for code filter
                        string[] values = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        filters[dimensionCode] = new CodeFilter(values);
                        break;

                    case "from":
                        filters[dimensionCode] = new FromFilter(value);
                        break;

                    case "to":
                        filters[dimensionCode] = new ToFilter { FilterString = value };
                        break;

                    case "first":
                        if (int.TryParse(value, out int firstCount) && firstCount > 0)
                        {
                            filters[dimensionCode] = new FirstFilter(firstCount);
                        }
                        else
                        {
                            throw new ArgumentException("Invalid value for 'first' filter. Must be a positive integer.");
                        }
                        break;

                    case "last":
                        if (int.TryParse(value, out int lastCount) && lastCount > 0)
                        {
                            filters[dimensionCode] = new LastFilter(lastCount);
                        }
                        else
                        {
                            throw new ArgumentException("Invalid value for 'last' filter. Must be a positive integer.");
                        }
                        break;

                    default:
                        // Unreached code, should not happen due to initial validation
                        throw new ArgumentException("Unexpected filter type.");
                }
            }
            return filters;
        }
    }
}