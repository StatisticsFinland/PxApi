using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using PxApi.Models.QueryFilters;
using Swashbuckle.AspNetCore.SwaggerGen;
using static PxApi.Models.QueryFilters.FilterJsonConverter;

namespace PxApi.Configuration
{
    /// <summary>
    /// Schema filter to provide proper OpenAPI documentation for Filter types.
    /// This ensures that the custom JSON serialization format used by FilterJsonConverter
    /// is properly documented in the OpenAPI schema.
    /// </summary>
    public class FilterSchemaFilter : ISchemaFilter
    {
        /// <summary>
        /// Applies the schema filter to map Filter types to their proper JSON representation.
        /// </summary>
        /// <param name="schema">The OpenAPI schema to modify.</param>
        /// <param name="context">The schema filter context containing type information.</param>
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type == typeof(Filter))
            {
                // Clear any auto-generated properties
                schema.Properties?.Clear();
                schema.AllOf?.Clear();
                schema.OneOf?.Clear();
                schema.AnyOf?.Clear();

                // Define the filter schema structure
                schema.Type = "object";
                schema.Required = new HashSet<string> { "type" };
                schema.Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["type"] = new OpenApiSchema
                    {
                        Type = "string",
                        Enum = [.. Enum.GetNames<FilterType>().Select(n => (IOpenApiAny)new OpenApiString(n))],
                        Description = "The type of filter to apply"
                    },
                    ["query"] = new OpenApiSchema
                    {
                        Description = "Filter-specific query parameter. Type varies based on filter type.",
                        OneOf =
                        [
                            // For Code filter: array of strings
                            new OpenApiSchema
                            {
                                Type = "array",
                                Items = new OpenApiSchema { Type = "string" },
                                Description = "Array of dimension value codes (for Code filter)"
                            },
                            // For From/To filters: single string
                            new OpenApiSchema
                            {
                                Type = "string",
                                Description = "Dimension value code to filter from/to (for From/To filters)"
                            },
                            // For First/Last filters: integer
                            new OpenApiSchema
                            {
                                Type = "integer",
                                Description = "Number of elements to take (for First/Last filters)"
                            }
                        ]
                    }
                };

                // Add examples for better documentation
                schema.Example = new OpenApiObject
                {
                    ["type"] = new OpenApiString("Code"),
                    ["query"] = new OpenApiArray
                    {
                        new OpenApiString("value1"),
                        new OpenApiString("value2")
                    }
                };
            }
            // Handle Dictionary<string, Filter> which is used in POST endpoints
            else if (context.Type == typeof(Dictionary<string, Filter>))
            {
                schema.Type = "object";
                schema.AdditionalProperties = new OpenApiSchema
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.Schema,
                        Id = nameof(Filter)
                    }
                };
                schema.Description = "Dictionary where keys are dimension codes and values are filter objects";
                
                // Add example for the dictionary structure
                schema.Example = new OpenApiObject
                {
                    ["dimensionCode1"] = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("Code"),
                        ["query"] = new OpenApiArray
                        {
                            new OpenApiString("value1"),
                            new OpenApiString("value2")
                        }
                    },
                    ["dimensionCode2"] = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("All")
                    },
                    ["dimensionCode3"] = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("First"),
                        ["query"] = new OpenApiInteger(5)
                    }
                };
            }
        }
    }
}