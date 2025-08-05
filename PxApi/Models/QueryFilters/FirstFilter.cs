using Px.Utils.Models.Metadata;

namespace PxApi.Models.QueryFilters
{
    /// <summary>
    /// Provides a filter that returns the first N elements from the input collection.
    /// </summary>
    public class FirstFilter(int count) : IFilter
    {
        /// <summary>
        /// Number of items to take from the start of the input enumeration.
        /// </summary>
        public int Count { get; } = count;

        /// <inheritdoc/>
        public DimensionMap Apply(IDimensionMap input) => new(input.Code, [.. input.ValueCodes.Take(Count)]);
    }
}
