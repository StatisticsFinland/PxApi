using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using static PxApi.Models.QueryFilters.FilterJsonConverter;

namespace PxApi.Configuration
{
    /// <summary>
    /// Enhances DataController POST endpoint documentation with request body examples and refined descriptions.
    /// </summary>
    public class DataControllerPostEndpointDocumentFilter : IDocumentFilter
    {
        /// <summary>
        /// Applies documentation enhancements to POST operations under /data.
        /// </summary>
        /// <param name="swaggerDoc">OpenAPI document.</param>
        /// <param name="context">Filter context.</param>
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            foreach (KeyValuePair<string, OpenApiPathItem> path in swaggerDoc.Paths)
            {
                if (path.Value.Operations.TryGetValue(OperationType.Post, out OpenApiOperation? postOp) && IsDataControllerPostOperation(path.Key, postOp))
                {
                    AddComprehensiveRequestBodyExamples(postOp);
                    AddResponseExamples(postOp);
                    RefineLanguageParameter(postOp);
                    AppendAcceptHeaderNote(postOp);
                }
            }

            if (swaggerDoc.Components?.Schemas?.TryGetValue("Filter", out OpenApiSchema? filterSchema) == true && string.IsNullOrWhiteSpace(filterSchema.Description))
            {
                filterSchema.Description = "Filter object. type determines behavior (Code | From | To | First | Last). query is array[string] (Code), string (From/To), integer>0 (First/Last). '*' wildcard matches zero or more characters.";
            }
        }

        private static void AddResponseExamples(OpenApiOperation operation)
        {
            if (operation.Responses.TryGetValue("200", out OpenApiResponse? response))
            {
                if (response.Content.TryGetValue("application/json", out OpenApiMediaType? jsonMediaType))
                {
                    jsonMediaType.Schema = new OpenApiSchema
                    {
                        Reference = new OpenApiReference { Id = "JsonStat2", Type = ReferenceType.Schema }
                    };
                    jsonMediaType.Example = JsonStat2Example.Instance;
                    if (string.IsNullOrWhiteSpace(response.Description))
                    {
                        response.Description = "Returns JSON-stat 2.0 dataset when 'Accept: application/json' or '*/*'. Use 'Accept: text/csv' for CSV output.";
                    }
                }
                if (response.Content.TryGetValue("text/csv", out OpenApiMediaType? csvMediaType) && csvMediaType.Schema != null && string.IsNullOrWhiteSpace(csvMediaType.Schema.Description))
                {
                    csvMediaType.Schema.Description = "CSV dataset (UTF-8, comma separated, header row). Column order follows dimension order then metric.";
                }
            }
        }

        private static void AddComprehensiveRequestBodyExamples(OpenApiOperation operation)
        {
            OpenApiRequestBody? requestBody = operation.RequestBody;
            if (requestBody?.Content == null) return;

            Dictionary<string, OpenApiExample> examples = new()
            {
                ["code-filter"] = new OpenApiExample
                {
                    Summary = "Code filter",
                    Description = "Specific codes, full wildcard, partial wildcard.",
                    Value = new OpenApiObject
                    {
                        ["gender"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.Code)),
                            ["query"] = new OpenApiArray { new OpenApiString("1") }
                        },
                        ["age"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.Code)),
                            ["query"] = new OpenApiArray { new OpenApiString("25-34"), new OpenApiString("35-44") }
                        },
                        ["region"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.Code)),
                            ["query"] = new OpenApiArray { new OpenApiString("*") }
                        },
                        ["category"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.Code)),
                            ["query"] = new OpenApiArray { new OpenApiString("*manufacturing*") }
                        }
                    }
                },
                ["from-filter"] = new OpenApiExample
                {
                    Summary = "From filter",
                    Description = "Inclusive start at value or pattern.",
                    Value = new OpenApiObject
                    {
                        ["year"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.From)),
                            ["query"] = new OpenApiString("2020")
                        },
                        ["time"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.From)),
                            ["query"] = new OpenApiString("202*")
                        }
                    }
                },
                ["to-filter"] = new OpenApiExample
                {
                    Summary = "To filter",
                    Description = "Inclusive end at value or pattern.",
                    Value = new OpenApiObject
                    {
                        ["year"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.To)),
                            ["query"] = new OpenApiString("2023")
                        },
                        ["time"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.To)),
                            ["query"] = new OpenApiString("2022*")
                        }
                    }
                },
                ["first-filter"] = new OpenApiExample
                {
                    Summary = "First filter",
                    Description = "First N values (N > 0).",
                    Value = new OpenApiObject
                    {
                        ["region"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.First)),
                            ["query"] = new OpenApiInteger(10)
                        }
                    }
                },
                ["last-filter"] = new OpenApiExample
                {
                    Summary = "Last filter",
                    Description = "Last N values (N > 0).",
                    Value = new OpenApiObject
                    {
                        ["region"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.Last)),
                            ["query"] = new OpenApiInteger(5)
                        }
                    }
                },
                ["combined-filters"] = new OpenApiExample
                {
                    Summary = "Combined filters",
                    Description = "Multiple filter types in one request.",
                    Value = new OpenApiObject
                    {
                        ["gender"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.Code)),
                            ["query"] = new OpenApiArray { new OpenApiString("1"), new OpenApiString("2") }
                        },
                        ["year"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.From)),
                            ["query"] = new OpenApiString("2020")
                        },
                        ["age"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.To)),
                            ["query"] = new OpenApiString("81-90")
                        },
                        ["region"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.First)),
                            ["query"] = new OpenApiInteger(3)
                        },
                        ["rooms"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.Last)),
                            ["query"] = new OpenApiInteger(2)
                        }
                    }
                }
            };

            foreach (OpenApiMediaType mediaType in requestBody.Content.Values)
            {
                mediaType.Examples = examples;
                if (mediaType.Schema != null && string.IsNullOrWhiteSpace(mediaType.Schema.Description))
                {
                    mediaType.Schema.Description = "Dictionary mapping dimension codes to filter objects (one per dimension).";
                }
            }
        }

        private static bool IsDataControllerPostOperation(string pathKey, OpenApiOperation operation)
        {
            bool isDataPath = pathKey.Equals("/data/{database}/{table}", StringComparison.OrdinalIgnoreCase);
            bool hasRequestBody = operation.RequestBody?.Content?.Any() ?? false;
            return isDataPath && hasRequestBody;
        }

        private static void RefineLanguageParameter(OpenApiOperation operation)
        {
            OpenApiParameter? langParam = operation.Parameters?.FirstOrDefault(p => p.Name == "lang");
            if (langParam != null)
            {
                langParam.Description = "Optional language code (ISO 639-1). Defaults to table's default language. Must be one of the table's AvailableLanguages.";
            }
        }

        private static void AppendAcceptHeaderNote(OpenApiOperation operation)
        {
            operation.Description = (operation.Description ?? string.Empty) +
                "Accept header options: application/json (JSON-stat), text/csv (CSV), */* treated as JSON-stat. Unsupported media types yield 406.";
        }
    }
}