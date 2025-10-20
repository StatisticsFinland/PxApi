using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using PxApi.Models.QueryFilters;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Diagnostics.CodeAnalysis;
using static PxApi.Models.QueryFilters.FilterJsonConverter;

namespace PxApi.Configuration
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
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type == typeof(Filter))
            {
                schema.Properties?.Clear();
                schema.AllOf?.Clear();
                schema.OneOf?.Clear();
                schema.AnyOf?.Clear();

                schema.Type = "object";
                schema.Required = new HashSet<string> { "type" };
                schema.Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["type"] = new OpenApiSchema
                    {
                        Type = "string",
                        Enum = [.. Enum.GetNames<FilterType>().Select(n => (IOpenApiAny)new OpenApiString(n))],
                        Description = "Filter type. Code | From | To | First | Last"
                    },
                    ["query"] = new OpenApiSchema
                    {
                        Description = "Filter-specific query value. Code: array[string] (supports '*' wildcard). From/To: string (supports '*'). First/Last: positive integer.",
                        OneOf =
                        [
                            new OpenApiSchema
                            {
                                Type = "array",
                                Items = new OpenApiSchema { Type = "string" },
                                Description = "Code filter: list of codes or wildcard patterns. Comma list in GET; array in POST body. '*' matches zero or more characters."
                            },
                            new OpenApiSchema
                            {
                                Type = "string",
                                Description = "From / To filters: single inclusive boundary value; wildcard '*' allowed."
                            },
                            new OpenApiSchema
                            {
                                Type = "integer",
                                Minimum = 1,
                                Description = "First / Last filters: positive count (N > 0)."
                            }
                        ]
                    }
                };

                schema.Example = new OpenApiObject
                {
                    ["type"] = new OpenApiString("Code"),
                    ["query"] = new OpenApiArray
                    {
                        new OpenApiString("A01"),
                        new OpenApiString("A02"),
                        new OpenApiString("*MANUF*")
                    }
                };
            }
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
                schema.Description = "Dictionary mapping dimension codes to filter objects (one per dimension).";

                schema.Example = new OpenApiObject
                {
                    ["gender"] = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("Code"),
                        ["query"] = new OpenApiArray
                        {
                            new OpenApiString("1"),
                            new OpenApiString("2")
                        }
                    },
                    ["year"] = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("From"),
                        ["query"] = new OpenApiString("2020")
                    },
                    ["region"] = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("First"),
                        ["query"] = new OpenApiInteger(5)
                    }
                };
            }
        }
    }
}