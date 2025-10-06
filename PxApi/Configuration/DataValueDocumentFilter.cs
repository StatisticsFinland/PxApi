using Microsoft.OpenApi.Models;
using Px.Utils.Models.Data;
using Px.Utils.Models.Data.DataValue;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PxApi.Configuration
{
    /// <summary>
    /// Document filter to remove DoubleDataValue and DataValueType component schemas from the OpenAPI document.
    /// Since these types are internal implementation details that are never returned to end users,
    /// they should not appear in the components section of the OpenAPI documentation.
    /// </summary>
    public class DataValueDocumentFilter : IDocumentFilter
    {
        private static readonly HashSet<string> DataValueTypeNames =
        [
            nameof(DoubleDataValue),
            nameof(DataValueType)
        ];

        /// <summary>
        /// Removes DoubleDataValue and DataValueType component schemas from the OpenAPI document.
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
                    // Remove any schema that matches DataValue type names
                    if (DataValueTypeNames.Any(typeName => 
                        schema.Key.Contains(typeName, StringComparison.OrdinalIgnoreCase)))
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