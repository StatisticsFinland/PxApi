using Microsoft.OpenApi.Models;
using PxApi.Models.QueryFilters;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PxApi.Configuration
{
    /// <summary>
    /// Document filter to remove Filter subclass component schemas from the OpenAPI document.
    /// Since Filter subclasses are serialized using FilterJsonConverter as polymorphic types,
    /// we only want the base Filter class to appear in the components section.
    /// </summary>
    public class FilterSubclassDocumentFilter : IDocumentFilter
    {
        private static readonly HashSet<string> FilterSubclassNames = new()
        {
            nameof(CodeFilter),
            nameof(FromFilter), 
            nameof(ToFilter),
            nameof(FirstFilter),
            nameof(LastFilter)
        };

        /// <summary>
        /// Removes Filter subclass component schemas from the OpenAPI document.
        /// </summary>
        /// <param name="swaggerDoc">The OpenAPI document to modify.</param>
        /// <param name="context">The document filter context.</param>
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            if (swaggerDoc.Components?.Schemas != null)
            {
                List<string> keysToRemove = [];
                foreach (KeyValuePair<string, OpenApiSchema> schema in swaggerDoc.Components.Schemas)
                {
                    // Remove any schema that matches Filter subclass names
                    if (FilterSubclassNames.Any(filterName => 
                        schema.Key.Contains(filterName, StringComparison.OrdinalIgnoreCase)))
                    {
                        keysToRemove.Add(schema.Key);
                    }
                }
                
                foreach (string key in keysToRemove)
                {
                    swaggerDoc.Components.Schemas.Remove(key);
                }
            }
        }
    }
}