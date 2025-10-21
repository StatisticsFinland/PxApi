using Microsoft.OpenApi.Models;
using Px.Utils.Models.Data;
using Px.Utils.Models.Data.DataValue;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PxApi.OpenApi.DocumentFilters
{
    /// <summary>
    /// Document filter that removes internal DataValue implementation detail schemas from the OpenAPI document.
    /// This consolidates the previous DoubleDataValueDocumentFilter and DataValueDocumentFilter into a single filter.
    /// DoubleDataValue is represented as a number via a custom converter, and DataValueType is an internal helper.
    /// Neither should appear under components for public API consumers.
    /// </summary>
    public class DataValueDocumentFilter : IDocumentFilter
    {
        private static readonly HashSet<string> DataValueTypeNames =
        [
            nameof(DoubleDataValue),
            nameof(DataValueType)
        ];

        /// <summary>
        /// Removes matching DataValue implementation schemas (e.g. DoubleDataValue, DataValueType) from the components section.
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
                    // Remove any schema whose key contains one of the internal DataValue type name fragments.
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