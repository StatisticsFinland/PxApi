using Microsoft.OpenApi;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using static PxApi.Models.QueryFilters.FilterJsonConverter;

namespace PxApi.OpenApi.Examples
{
    /// <summary>
    /// Provides OpenAPI request body examples for the DataController POST endpoint filters object structure.
    /// </summary>
    [SuppressMessage("SonarAnalyzer.CSharp", "S1192", Justification = "Duplicate string literals are intentional to represent example JSON structure.")]
    public static class DataRequestBodyExamples
    {
        /// <summary>
        /// Gets the predefined request body examples keyed by example identifier.
        /// </summary>
        public static IReadOnlyDictionary<string, OpenApiExample> Examples { get; } = new Dictionary<string, OpenApiExample>
        {
            ["code-filter"] = new OpenApiExample
            {
                Summary = "Code filter",
                Description = "Specific codes, full wildcard, partial wildcard.",
                Value = new JsonObject
                {
                    ["gender"] = new JsonObject
                    {
                        ["type"] = nameof(FilterType.Code),
                        ["query"] = new JsonArray("1")
                    },
                    ["age"] = new JsonObject
                    {
                        ["type"] = nameof(FilterType.Code),
                        ["query"] = new JsonArray("25-34", "35-44")
                    },
                    ["region"] = new JsonObject
                    {
                        ["type"] = nameof(FilterType.Code),
                        ["query"] = new JsonArray("*")
                    },
                    ["category"] = new JsonObject
                    {
                        ["type"] = nameof(FilterType.Code),
                        ["query"] = new JsonArray("*manufacturing*")
                    }
                }
            },
            ["from-filter"] = new OpenApiExample
            {
                Summary = "From filter",
                Description = "Inclusive start at value or pattern.",
                Value = new JsonObject
                {
                    ["year"] = new JsonObject
                    {
                        ["type"] = nameof(FilterType.From),
                        ["query"] = "2020"
                    },
                    ["time"] = new JsonObject
                    {
                        ["type"] = nameof(FilterType.From),
                        ["query"] = "202*"
                    }
                }
            },
            ["to-filter"] = new OpenApiExample
            {
                Summary = "To filter",
                Description = "Inclusive end at value or pattern.",
                Value = new JsonObject
                {
                    ["year"] = new JsonObject
                    {
                        ["type"] = nameof(FilterType.To),
                        ["query"] = "2023"
                    },
                    ["time"] = new JsonObject
                    {
                        ["type"] = nameof(FilterType.To),
                        ["query"] = "2022*"
                    }
                }
            },
            ["first-filter"] = new OpenApiExample
            {
                Summary = "First filter",
                Description = "First N values (N > 0).",
                Value = new JsonObject
                {
                    ["region"] = new JsonObject
                    {
                        ["type"] = nameof(FilterType.First),
                        ["query"] = 10
                    }
                }
            },
            ["last-filter"] = new OpenApiExample
            {
                Summary = "Last filter",
                Description = "Last N values (N > 0).",
                Value = new JsonObject
                {
                    ["region"] = new JsonObject
                    {
                        ["type"] = nameof(FilterType.Last),
                        ["query"] = 5
                    }
                }
            },
            ["combined-filters"] = new OpenApiExample
            {
                Summary = "Combined filters",
                Description = "Multiple filter types in one request.",
                Value = new JsonObject
                {
                    ["gender"] = new JsonObject
                    {
                        ["type"] = nameof(FilterType.Code),
                        ["query"] = new JsonArray("1", "2")
                    },
                    ["year"] = new JsonObject
                    {
                        ["type"] = nameof(FilterType.From),
                        ["query"] = "2020"
                    },
                    ["age"] = new JsonObject
                    {
                        ["type"] = nameof(FilterType.To),
                        ["query"] = "81-90"
                    },
                    ["region"] = new JsonObject
                    {
                        ["type"] = nameof(FilterType.First),
                        ["query"] = 3
                    },
                    ["rooms"] = new JsonObject
                    {
                        ["type"] = nameof(FilterType.Last),
                        ["query"] = 2
                    }
                }
            }
        };
    }
}
