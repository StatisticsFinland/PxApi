using Microsoft.OpenApi;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;

namespace PxApi.OpenApi.Examples
{
    /// <summary>
    /// Provides OpenAPI examples for the 'filters' query parameter of the data GET endpoint.
    /// </summary>
    public static class FiltersParameterExamples
    {
        /// <summary>
        /// Gets the predefined 'filters' parameter examples keyed by example identifier.
        /// </summary>
        [SuppressMessage("SonarAnalyzer.CSharp", "S1192", Justification = "Duplicate string literals represent example parameter values.")]
        public static IReadOnlyDictionary<string, OpenApiExample> Examples { get; } = new Dictionary<string, OpenApiExample>
        {
            ["code-filter"] = new OpenApiExample
            {
                Summary = "Code filter",
                Description = "Single gender, multiple ages, full wildcard region, partial wildcard category.",
                Value = new JsonArray(
                    "gender:code=1",
                    "age:code=25-34,35-44",
                    "region:code=*",
                    "category:code=*manufacturing*"
                )
            },
            ["from-filter"] = new OpenApiExample
            {
                Summary = "From filter",
                Description = "Years from 2020 onward; time codes starting with 202.",
                Value = new JsonArray(
                    "year:from=2020",
                    "time:from=202*"
                )
            },
            ["to-filter"] = new OpenApiExample
            {
                Summary = "To filter",
                Description = "Years up to 2023; time codes up to first match starting with 2022.",
                Value = new JsonArray(
                    "year:to=2023",
                    "time:to=2022*"
                )
            },
            ["first-filter"] = new OpenApiExample
            {
                Summary = "First filter",
                Description = "First 10 region codes.",
                Value = new JsonArray("region:first=10")
            },
            ["last-filter"] = new OpenApiExample
            {
                Summary = "Last filter",
                Description = "Last 5 region codes.",
                Value = new JsonArray("region:last=5")
            },
            ["combined-filters"] = new OpenApiExample
            {
                Summary = "Combined filters",
                Description = "Multiple types together.",
                Value = new JsonArray(
                    "gender:code=1,2",
                    "year:from=2020",
                    "age:to=81-90",
                    "region:first=3",
                    "rooms:last=2"
                )
            }
        };
    }
}
