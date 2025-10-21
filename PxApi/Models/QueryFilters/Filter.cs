using Px.Utils.Models.Metadata;
using System.Diagnostics.CodeAnalysis;

namespace PxApi.Models.QueryFilters
{
    /// <summary>
    /// Represents a base class for filters that process a collection of dimension value codes.
    /// </summary>
    [SuppressMessage("Minor Code Smell", "S1694:An abstract class should have both abstract and concrete methods", Justification = "This needs to stay as a class to work with OpenApi documentation.")]
    public abstract class Filter
    {
        /// <summary>
        /// Returns a new <see cref="IDimensionMap"/> that contains the filtered dimension values based on the input.
        /// </summary>
        /// <param name="input">The input dimension map to filter.</param>
        /// <returns>A new <see cref="IDimensionMap"/> that contains the filtered dimension values.</returns>
        public abstract DimensionMap Apply(IDimensionMap input);

        /// <summary>
        /// Gets the parameter name for this filter type.
        /// </summary>
        /// <remarks>
        /// This instance property allows accessing the parameter name from a filter instance.
        /// Each derived class should override this to return their specific parameter name.
        /// </remarks>
        public virtual string ParamName => "filter";
    }
}
