using Px.Utils.Models.Metadata;

namespace PxApi.Models.QueryFilters
{
    /// <summary>
    /// Represents a base class for filters that process a collection of dimension value codes.
    /// </summary>
    public abstract class Filter
    {
        /// <summary>
        /// Returns a new <see cref="IDimensionMap"/> that contains the filtered dimension values based on the input.
        /// </summary>
        /// <param name="input">The input dimension map to filter.</param>
        /// <returns>A new <see cref="IDimensionMap"/> that contains the filtered dimension values.</returns>
        public abstract DimensionMap Apply(IDimensionMap input);
    }
}
