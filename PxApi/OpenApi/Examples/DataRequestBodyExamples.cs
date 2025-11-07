using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using System.Diagnostics.CodeAnalysis;
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
                Description = "First N values (N >0).",
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
                Description = "Last N values (N >0).",
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
    }
}
