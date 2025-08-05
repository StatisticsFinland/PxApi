
using Px.Utils.Models.Metadata;

namespace PxApi.Models.QueryFilters
{
    /// <summary>
    /// A filter that returns the last N elements from the input collection.
    /// </summary>
    // TODO: Needs to be tested if count is larger than the input value amount. This should return all values.
    public class LastFilter(int count) : IFilter
    {
        /// <summary>
        /// Number of items to take from the end of the input enumeration.
        /// </summary>
        public int Count { get; } = count;

        /// <inheritdoc/>
        public DimensionMap Apply(IDimensionMap input) => new(
            input.Code,
            [.. input.ValueCodes.Skip(Math.Max(0, input.ValueCodes.Count - Count))]);
    }
}
