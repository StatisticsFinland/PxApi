using Px.Utils.Models.Metadata;
using Px.Utils.Models.Metadata.Dimensions;

namespace PxApi.Models.QueryFilters
{
    /// <summary>
    /// Collection of utility methods used in the various query filters.
    /// </summary>
    public static class FilterUtils
    {
        /// <summary>
        /// Determines whether the input string matches the given filter pattern, where '*' in the filter matches zero or more characters.
        /// The comparison is case-insensitive and supports multiple wildcards in the filter.
        /// </summary>
        public static bool IsCodeMatch(string input, string filter)
        {
            input = input.ToLower();
            filter = filter.ToLower();

            int filterIndx = 0;
            int patternStart = -1;
            for(int i = 0; i < input.Length; i++)
            {
                while (filter[filterIndx] == '*')
                {
                    patternStart = filterIndx;
                    filterIndx++;
                    if (filterIndx >= filter.Length) return true;
                }

                if (filter[filterIndx] == input[i])
                {
                    filterIndx++;
                    if (filterIndx >= filter.Length)
                    {
                        if(i >= input.Length - 1) return true;
                        if (patternStart < 0) return false;
                        filterIndx = patternStart;
                    }
                }
                else
                {
                    if (patternStart < 0) return false;
                    filterIndx = patternStart;
                }
            }

            while (filterIndx < filter.Length && filter[filterIndx] == '*')
            {
                filterIndx++;
            }

            return filterIndx >= filter.Length;
        }

        /// <summary>
        /// Applies all filters to each dimension in the metadata and returns a MatrixMap with the combined results.
        /// </summary>
        /// <param name="meta">The metadata containing dimensions to filter</param>
        /// <param name="filters">Dictionary of dimension codes and their corresponding filters</param>
        /// <returns>A MatrixMap containing the filtered dimensions</returns>
        public static MatrixMap FilterDimensions(IReadOnlyMatrixMetadata meta, Dictionary<string, List<Filter>> filters)
        {
            List<IDimensionMap> dimMaps = [];
            
            foreach(IReadOnlyDimension dim in meta.Dimensions)
            {
                IDimensionMap dimensionMap = new DimensionMap(dim.Code, [..dim.ValueCodes]);
                if (filters.TryGetValue(dim.Code, out List<Filter>? filterList))
                {
                    foreach (Filter filter in filterList)
                    {
                        dimensionMap = filter.Apply(dimensionMap);
                    }
                }
                dimMaps.Add(dimensionMap);
            }

            return new MatrixMap(dimMaps);
        }
    }
}
