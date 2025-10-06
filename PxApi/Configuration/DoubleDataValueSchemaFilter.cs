using Microsoft.OpenApi.Models;
using Px.Utils.Models.Data.DataValue;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PxApi.Configuration
{
    /// <summary>
    /// Schema filter to map DoubleDataValue to number type in OpenAPI documentation.
    /// This ensures that DoubleDataValue properties appear as number types in the generated
    /// OpenAPI schema, matching the actual JSON serialization behavior of the DoubleDataValueJsonConverter.
    /// Component removal is handled by DataValueDocumentFilter.
    /// </summary>
    public class DoubleDataValueSchemaFilter : ISchemaFilter
    {
        /// <summary>
        /// Applies the schema filter to map DoubleDataValue types to number schema.
        /// </summary>
        /// <param name="schema">The OpenAPI schema to modify.</param>
        /// <param name="context">The schema filter context containing type information.</param>
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            // Handle individual DoubleDataValue types
            if (context.Type == typeof(DoubleDataValue))
            {
                schema.Type = "number";
                schema.Format = "double";
                schema.Nullable = true;
                schema.Properties?.Clear();
                schema.AllOf?.Clear();
                schema.OneOf?.Clear();
                schema.AnyOf?.Clear();
                schema.AdditionalProperties = null;
                schema.Reference = null;
            }
            // Handle DoubleDataValue arrays
            else if (context.Type == typeof(DoubleDataValue[]))
            {
                schema.Type = "array";
                schema.Items = new OpenApiSchema
                {
                    Type = "number",
                    Format = "double",
                    Nullable = true,
                    Reference = null
                };
                schema.Properties?.Clear();
                schema.AllOf?.Clear();
                schema.OneOf?.Clear();
                schema.AnyOf?.Clear();
                schema.Reference = null;
            }
        }
    }
}