using Microsoft.OpenApi;
using Px.Utils.Models.Data.DataValue;
using PxApi.Models.JsonStat;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PxApi.OpenApi.SchemaFilters
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
        public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
        {
            if (schema is not OpenApiSchema concreteSchema)
            {
                return;
            }

            // Handle individual DoubleDataValue types
            if (context.Type == typeof(DoubleDataValue))
            {
                concreteSchema.Type = JsonSchemaType.Number | JsonSchemaType.Null;
                concreteSchema.Format = "double";
                concreteSchema.Properties?.Clear();
                concreteSchema.AllOf?.Clear();
                concreteSchema.OneOf?.Clear();
                concreteSchema.AnyOf?.Clear();
                concreteSchema.AdditionalProperties = null;
            }
            // Handle DoubleDataValue arrays
            else if (context.Type == typeof(DoubleDataValue[]) || context.Type == typeof(PrecisionDataArray))
            {
                concreteSchema.Type = JsonSchemaType.Array;
                concreteSchema.Items = new OpenApiSchema
                {
                    Type = JsonSchemaType.Number | JsonSchemaType.Null,
                    Format = "double"
                };
                concreteSchema.Properties?.Clear();
                concreteSchema.AllOf?.Clear();
                concreteSchema.OneOf?.Clear();
                concreteSchema.AnyOf?.Clear();
            }
        }
    }
}