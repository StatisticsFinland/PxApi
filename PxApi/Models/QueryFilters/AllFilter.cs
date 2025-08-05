using Px.Utils.Models.Metadata;

namespace PxApi.Models.QueryFilters
{
    /// <summary>
    /// A filter that returns all elements from the input collection.
    /// </summary>
    public class AllFilter : IFilter
    {
        /// <inheritdoc/>
        public DimensionMap Apply(IDimensionMap input) => new(input);
    }
}
