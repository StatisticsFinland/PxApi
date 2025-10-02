using Px.Utils.Models.Metadata;

namespace PxApi.Models.QueryFilters
{
    /// <summary>
    /// Provides a filter that returns all elements from the input collection up to and including the first element that matches the specified filter string.
    /// The filter string can contain wildcard '*' that matches zero or more characters.
    /// </summary>
    public class ToFilter : Filter
    {
        /// <summary>
        /// Used to match the items in the input collection against, can contain wildcard '*' that matches zore or more characters.
        /// </summary>
        public required string FilterString { get; init; }

        /// <inheritdoc/>
        public override DimensionMap Apply(IDimensionMap input)
        {
            List<string> resultCodes = [];
            bool found = false;
            foreach (string s in input.ValueCodes)
            {
                resultCodes.Add(s);
                if (FilterUtils.IsCodeMatch(s, FilterString))
                {
                    found = true;
                    break;
                }
            }

            if (!found) throw new InvalidOperationException("No element matching the filterstring found.");
            return new DimensionMap(input.Code, resultCodes);
        }

        /// <summary>
        /// Parameter name constant for use in switch statements and static contexts
        /// </summary>
        public const string FilterTypeName = "to";

        /// <inheritdoc/>
        public override string ParamName => FilterTypeName;
    }
}
