using Microsoft.OpenApi.Models;
using Px.Utils.Models.Data.DataValue;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PxApi.OpenApi.DocumentFilters
{
    /// <summary>
    /// Document filter to remove DoubleDataValue component schemas from the OpenAPI document.
    /// Since DoubleDataValue is serialized as a number via custom converter, we don't want
    /// the complex type definition to appear in the components section.
    /// </summary>
    public class DoubleDataValueDocumentFilter : IDocumentFilter
    {
        /// <summary>
        /// Removes DoubleDataValue component schemas from the OpenAPI document.
        /// </summary>
        /// <param name="swaggerDoc">The OpenAPI document to modify.</param>
        /// <param name="context">The document filter context.</param>
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            const string doubleDataValueTypeName = nameof(DoubleDataValue);
            if (swaggerDoc.Components?.Schemas != null)
            {
                List<string> keysToRemove = [];
                foreach (KeyValuePair<string, OpenApiSchema> schema in swaggerDoc.Components.Schemas)
                {
                    if (schema.Key.Contains(doubleDataValueTypeName, StringComparison.OrdinalIgnoreCase))
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