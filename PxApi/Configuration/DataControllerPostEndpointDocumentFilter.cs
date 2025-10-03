using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using static PxApi.Models.QueryFilters.FilterJsonConverter;

namespace PxApi.Configuration
{
    /// <summary>
    /// Document filter to enhance DataController POST endpoint documentation with detailed request body examples.
    /// This provides comprehensive examples for POST endpoints that accept Filter dictionaries in data retrieval operations.
    /// </summary>
    public class DataControllerPostEndpointDocumentFilter : IDocumentFilter
    {
        // Parameter descriptions for OpenAPI documentation
        private const string DatabaseParameterDescription = "Name of the database containing the table";
        private const string TableParameterDescription = "Name of the px table to query";
        private const string QueryParameterDescription = "Dictionary containing dimension codes as keys and Filter objects as values. Each key represents a dimension code, and each value is a filter object that determines which dimension values to include. Supports various filter types including Code (specific values or patterns), From/To (range filtering), First/Last (top/bottom N values), and wildcard matching.";
        private const string LanguageParameterDescription = "Language code for the response. If not provided, uses the default language of the table";

        /// <summary>
        /// Applies enhanced documentation to DataController POST endpoints.
        /// </summary>
        /// <param name="swaggerDoc">The OpenAPI document to modify.</param>
        /// <param name="context">The document filter context.</param>
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            // Add detailed examples for DataController POST endpoints
            foreach (KeyValuePair<string, OpenApiPathItem> path in swaggerDoc.Paths)
            {
                if (path.Value.Operations.TryGetValue(OperationType.Post, out OpenApiOperation? postOp))
                {
                    // Check if this is a DataController POST operation
                    if (IsDataControllerPostOperation(path.Key, postOp))
                    {
                        AddComprehensiveRequestBodyExamples(postOp);
                        AddParameterDescriptions(postOp);
                    }
                }
            }

            // Add additional schema descriptions if they don't exist
            if (swaggerDoc.Components?.Schemas?.ContainsKey("Filter") == true)
            {
                OpenApiSchema filterSchema = swaggerDoc.Components.Schemas["Filter"];
                if (string.IsNullOrEmpty(filterSchema.Description))
                {
                    filterSchema.Description = 
                        "Filter object for selecting dimension values. The type field determines the filter behavior, " +
                        "and the query field provides the filter-specific parameters.";
                }
            }
        }

        /// <summary>
        /// Adds parameter descriptions to POST operations.
        /// </summary>
        /// <param name="operation">The POST operation to enhance with parameter descriptions.</param>
        private static void AddParameterDescriptions(OpenApiOperation operation)
        {
            if (operation.Parameters == null) return;

            foreach (OpenApiParameter parameter in operation.Parameters)
            {
                if (string.IsNullOrEmpty(parameter.Description))
                {
                    parameter.Description = parameter.Name.ToLowerInvariant() switch
                    {
                        "database" => DatabaseParameterDescription,
                        "table" => TableParameterDescription,
                        "lang" => LanguageParameterDescription,
                        _ => parameter.Description
                    };
                }
            }

            // Add description for request body if it exists
            if (operation.RequestBody?.Content?.ContainsKey("application/json") == true)
            {
                OpenApiMediaType mediaType = operation.RequestBody.Content["application/json"];
                if (mediaType.Schema != null && string.IsNullOrEmpty(mediaType.Schema.Description))
                {
                    mediaType.Schema.Description = QueryParameterDescription;
                }
            }
        }

        /// <summary>
        /// Adds comprehensive request body examples to POST operations.
        /// </summary>
        /// <param name="operation">The POST operation to enhance with examples.</param>
        private static void AddComprehensiveRequestBodyExamples(OpenApiOperation operation)
        {
            OpenApiRequestBody? requestBody = operation.RequestBody;
            if (requestBody?.Content?.ContainsKey("application/json") != true) return;

            OpenApiMediaType mediaType = requestBody.Content["application/json"];

            // Add comprehensive examples for different filter scenarios
            mediaType.Examples = new Dictionary<string, OpenApiExample>
            {
                ["simple-code-filter"] = new OpenApiExample
                {
                    Summary = "Simple code filter",
                    Description = "Filter specific dimension values by their codes - basic selection",
                    Value = new OpenApiObject
                    {
                        ["Region"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.Code)),
                            ["query"] = new OpenApiArray
                                {
                                    new OpenApiString("01"),
                                    new OpenApiString("02"),
                                    new OpenApiString("05")
                                }
                        }
                    }
                },
                ["demographic-analysis"] = new OpenApiExample
                {
                    Summary = "Demographic analysis",
                    Description = "Complex demographic analysis with age groups, gender, and recent time periods",
                    Value = new OpenApiObject
                    {
                        ["Age"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.Code)),
                            ["query"] = new OpenApiArray
                            {
                                new OpenApiString("25-34"),
                                new OpenApiString("35-44"),
                                new OpenApiString("45-54")
                            }
                        },
                        ["Gender"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.Code)),
                            ["query"] = new OpenApiArray
                            {
                                new OpenApiString("1"),
                                new OpenApiString("2")
                            }
                        },
                        ["Year"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.Last)),
                            ["query"] = new OpenApiInteger(3)
                        },
                        ["Region"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.Code)),
                            ["query"] = new OpenApiArray
                            {
                                new OpenApiString("*")
                            }
                        }
                    }
                },
                ["regional-comparison"] = new OpenApiExample
                {
                    Summary = "Regional comparison",
                    Description = "Compare regions for a specific time period with economic indicators",
                    Value = new OpenApiObject
                    {
                        ["Region"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.First)),
                            ["query"] = new OpenApiInteger(10)
                        },
                        ["Year"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.From)),
                            ["query"] = new OpenApiString("2020")
                        },
                        ["Indicator"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.Code)),
                            ["query"] = new OpenApiArray
                            {
                                new OpenApiString("GDP"),
                                new OpenApiString("EMPLOYMENT"),
                                new OpenApiString("POPULATION")
                            }
                        }
                    }
                },
                ["time-series-analysis"] = new OpenApiExample
                {
                    Summary = "Time series analysis",
                    Description = "Get comprehensive time series data for specific regions and all available dimension values",
                    Value = new OpenApiObject
                    {
                        ["Region"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.Code)),
                            ["query"] = new OpenApiArray
                            {
                                new OpenApiString("Helsinki"),
                                new OpenApiString("Tampere"),
                                new OpenApiString("Turku"),
                                new OpenApiString("Oulu")
                            }
                        },
                        ["ClassificatoryDimension"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.Code)),
                            ["query"] = new OpenApiArray
                            {
                                new OpenApiString("*")
                            }
                        },
                        ["Year"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.From)),
                            ["query"] = new OpenApiString("2015")
                        }
                    }
                },
                ["mixed-filters-advanced"] = new OpenApiExample
                {
                    Summary = "Advanced mixed filter types",
                    Description = "Complex example using all filter types",
                    Value = new OpenApiObject
                    {
                        ["Region"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.Code)),
                            ["query"] = new OpenApiArray
                                {
                                    new OpenApiString("01"),
                                    new OpenApiString("02"),
                                    new OpenApiString("05"),
                                    new OpenApiString("10")
                                }
                        },
                        ["Year"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.From)),
                            ["query"] = new OpenApiString("2020")
                        },
                        ["Quarter"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.To)),
                            ["query"] = new OpenApiString("Q4")
                        },
                        ["TopCategories"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.First)),
                            ["query"] = new OpenApiInteger(5)
                        },
                        ["RecentData"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.Last)),
                            ["query"] = new OpenApiInteger(12)
                        },
                        ["ClassificatoryDimension"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.Code)),
                            ["query"] = new OpenApiArray
                            {
                                new OpenApiString("*")
                            }
                        }
                    }
                },
                ["category-breakdown"] = new OpenApiExample
                {
                    Summary = "Industry category breakdown",
                    Description = "Detailed breakdown by industry categories with pattern matching and wildcards",
                    Value = new OpenApiObject
                    {
                        ["Industry"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.Code)),
                            ["query"] = new OpenApiArray
                            {
                                new OpenApiString("*manufacturing*"),
                                new OpenApiString("*technology*"),
                                new OpenApiString("*services*")
                            }
                        },
                        ["Region"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.Last)),
                            ["query"] = new OpenApiInteger(5)
                        },
                        ["Year"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.Code)),
                            ["query"] = new OpenApiArray
                            {
                                new OpenApiString("2022"),
                                new OpenApiString("2023")
                            }
                        },
                        ["Size"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.Code)),
                            ["query"] = new OpenApiArray
                            {
                                new OpenApiString("SMALL"),
                                new OpenApiString("MEDIUM"),
                                new OpenApiString("LARGE")
                            }
                        }
                    }
                },
                ["range-filters-comprehensive"] = new OpenApiExample
                {
                    Summary = "Comprehensive range and count filters",
                    Description = "Example using From, To, First, and Last filters",
                    Value = new OpenApiObject
                    {
                        ["Year"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.From)),
                            ["query"] = new OpenApiString("2015")
                        },
                        ["Month"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.To)),
                            ["query"] = new OpenApiString("12")
                        },
                        ["TopPerformers"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.First)),
                            ["query"] = new OpenApiInteger(10)
                        },
                        ["RecentPeriods"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.Last)),
                            ["query"] = new OpenApiInteger(6)
                        },
                        ["ClassificatoryDimension"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.Code)),
                            ["query"] = new OpenApiArray
                            {
                                new OpenApiString("REVENUE"),
                                new OpenApiString("PROFIT"),
                                new OpenApiString("EMPLOYEES")
                            }
                        }
                    }
                },
                ["wildcard-comprehensive"] = new OpenApiExample
                {
                    Summary = "Comprehensive wildcard usage",
                    Description = "Advanced use of wildcards for pattern matching and selecting all values across multiple dimensions",
                    Value = new OpenApiObject
                    {
                        ["AllRegions"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.Code)),
                            ["query"] = new OpenApiArray
                            {
                                new OpenApiString("*")
                            }
                        },
                        ["TechSector"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.Code)),
                            ["query"] = new OpenApiArray
                            {
                                new OpenApiString("*tech*"),
                                new OpenApiString("*IT*"),
                                new OpenApiString("*digital*")
                            }
                        },
                        ["AllYears"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.Code)),
                            ["query"] = new OpenApiArray
                            {
                                new OpenApiString("*")
                            }
                        },
                        ["ManufacturingPatterns"] = new OpenApiObject
                        {
                            ["type"] = new OpenApiString(nameof(FilterType.Code)),
                            ["query"] = new OpenApiArray
                            {
                                new OpenApiString("*manufacturing*"),
                                new OpenApiString("*production*"),
                                new OpenApiString("*industrial*")
                            }
                        }
                    }
                }
            };

            // Enhance the description
            if (string.IsNullOrEmpty(mediaType.Schema?.Description))
            {
                if (mediaType.Schema != null)
                {
                    mediaType.Schema.Description =
                        "Query filters for data selection. Each key represents a dimension code, " +
                        "and each value is a filter object that determines which dimension values to include. " +
                        "Supports various filter types including Code (specific values or patterns), " +
                        "From/To (range filtering), First/Last (top/bottom N values), and wildcard matching.";
                }
            }
        }

        /// <summary>
        /// Determines if this is a DataController POST operation that accepts filter dictionaries.
        /// </summary>
        /// <param name="pathKey">The path key from the OpenAPI document.</param>
        /// <param name="operation">The POST operation to check.</param>
        /// <returns>True if this is a DataController POST operation that accepts filter dictionaries.</returns>
        private static bool IsDataControllerPostOperation(string pathKey, OpenApiOperation operation)
        {
            // Check if the path matches DataController POST endpoints:
            // - /data/json/{database}/{table}
            // - /{database}/{table}/json-stat
            bool hasJsonPath = pathKey.Contains("/json/") || pathKey.Contains("/json-stat");
            bool hasRequestBody = operation.RequestBody?.Content?.ContainsKey("application/json") == true;
            
            return hasJsonPath && hasRequestBody;
        }
    }
}