using Px.Utils.Models.Metadata;

namespace PxApi.Models.QueryFilters
{
    /// <summary>
    /// A filter that returns all elements from the input collection.
    /// </summary>
    public class AllFilter : Filter
    {
        /// <inheritdoc/>
        public override DimensionMap Apply(IDimensionMap input) => new(input);
    }
}
