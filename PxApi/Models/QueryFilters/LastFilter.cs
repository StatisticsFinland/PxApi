
using Px.Utils.Models.Metadata;

namespace PxApi.Models.QueryFilters
{
    /// <summary>
    /// A filter that returns the last N elements from the input collection.
    /// </summary>
    public class LastFilter(int count) : Filter
    {
        /// <summary>
        /// Number of items to take from the end of the input enumeration.
        /// </summary>
        public int Count { get; } = count;

        /// <inheritdoc/>
        public override DimensionMap Apply(IDimensionMap input) => new(
            input.Code,
            [.. input.ValueCodes.Skip(Math.Max(0, input.ValueCodes.Count - Count))]);

        /// <summary>
        /// Parameter name constant for use in switch statements and static contexts
        /// </summary>
        public const string FilterTypeName = "last";

        /// <inheritdoc/>
        public override string ParamName => FilterTypeName;
    }
}
