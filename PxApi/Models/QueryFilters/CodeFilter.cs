using Px.Utils.Models.Metadata;

namespace PxApi.Models.QueryFilters
{
    /// <summary>
    /// Provides filtering functionality for collections of strings using wildcard patterns.
    /// Supports the '*' character as a wildcard that matches zero or more characters.
    /// </summary>
    public class CodeFilter(IEnumerable<string> filterStrings) : Filter
    {
        /// <summary>
        /// Collection of filter strings that can include wildcard patterns to match against input strings.
        /// </summary>
        public List<string> FilterStrings { get; } = [.. filterStrings];

        /// <inheritdoc/>
        public override DimensionMap Apply(IDimensionMap input)
        {
            return new DimensionMap(
                input.Code,
                [.. input.ValueCodes.Where(c =>
                    FilterStrings.Any(f => FilterUtils.IsCodeMatch(c,f)))]);
        }

        /// <summary>
        /// Parameter name constant for use in switch statements and static contexts
        /// </summary>
        public const string FilterTypeName = "code";

        /// <inheritdoc/>
        public override string ParamName => FilterTypeName;
    }
}
