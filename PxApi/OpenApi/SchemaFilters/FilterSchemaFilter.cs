using Microsoft.OpenApi;
using PxApi.Models.QueryFilters;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using static PxApi.Models.QueryFilters.FilterJsonConverter;

namespace PxApi.OpenApi.SchemaFilters
{
    /// <summary>
    /// Schema filter to provide proper OpenAPI documentation for Filter types.
    /// Documents polymorphic shape and per-filter query value expectations.
    /// </summary>
    public class FilterSchemaFilter : ISchemaFilter
    {
        /// <summary>
        /// Applies the schema filter to map Filter types to their proper JSON representation.
        /// </summary>
        /// <param name="schema">The OpenAPI schema to modify.</param>
        /// <param name="context">The schema filter context containing type information.</param>
        [SuppressMessage("SonarAnalyzer.CSharp", "S1192", Justification = "Duplicate string literals are intentional to represent example JSON structure.")]
        public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
        {
            if (schema is not OpenApiSchema concreteSchema)
            {
                return;
            }

            if (context.Type == typeof(Filter))
            {
                concreteSchema.Properties?.Clear();
                concreteSchema.AllOf?.Clear();
                concreteSchema.OneOf?.Clear();
                concreteSchema.AnyOf?.Clear();

                concreteSchema.Type = JsonSchemaType.Object;
                concreteSchema.Required = new HashSet<string> { "type" };
                concreteSchema.Properties = new Dictionary<string, IOpenApiSchema>
                {
                    ["type"] = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                        Enum = [.. Enum.GetNames<FilterType>().Select(n => (JsonNode)n)],
                        Description = "Filter type. Code | From | To | First | Last"
                    },
                    ["query"] = new OpenApiSchema
                    {
                        Description = "Filter-specific query value. Code: array[string] (supports '*' wildcard). From/To: string (supports '*'). First/Last: positive integer.",
                        OneOf =
                        [
                            new OpenApiSchema
                            {
                                Type = JsonSchemaType.Array,
                                Items = new OpenApiSchema { Type = JsonSchemaType.String },
                                Description = "Code filter: list of codes or wildcard patterns. Comma list in GET; array in POST body. '*' matches zero or more characters."
                            },
                            new OpenApiSchema
                            {
                                Type = JsonSchemaType.String,
                                Description = "From / To filters: single inclusive boundary value; wildcard '*' allowed."
                            },
                            new OpenApiSchema
                            {
                                Type = JsonSchemaType.Integer,
                                Minimum = "1",
                                Description = "First / Last filters: positive count (N > 0)."
                            }
                        ]
                    }
                };

                concreteSchema.Example = new JsonObject
                {
                    ["type"] = "Code",
                    ["query"] = new JsonArray("A01", "A02", "*MANUF*")
                };
            }
            else if (context.Type == typeof(Dictionary<string, Filter>))
            {
                concreteSchema.Type = JsonSchemaType.Object;
                concreteSchema.AdditionalProperties = new OpenApiSchemaReference(nameof(Filter));
                concreteSchema.Description = "Dictionary mapping dimension codes to filter objects (one per dimension).";

                concreteSchema.Example = new JsonObject
                {
                    ["gender"] = new JsonObject
                    {
                        ["type"] = "Code",
                        ["query"] = new JsonArray("1", "2")
                    },
                    ["year"] = new JsonObject
                    {
                        ["type"] = "From",
                        ["query"] = "2020"
                    },
                    ["region"] = new JsonObject
                    {
                        ["type"] = "First",
                        ["query"] = 5
                    }
                };
            }
        }
    }
}