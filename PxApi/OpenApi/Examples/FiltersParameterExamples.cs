using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using System.Diagnostics.CodeAnalysis;

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
                Value = new OpenApiArray
                {
                    new OpenApiString("gender:code=1"),
                    new OpenApiString("age:code=25-34,35-44"),
                    new OpenApiString("region:code=*"),
                    new OpenApiString("category:code=*manufacturing*")
                }
            },
            ["from-filter"] = new OpenApiExample
            {
                Summary = "From filter",
                Description = "Years from 2020 onward; time codes starting with 202.",
                Value = new OpenApiArray
                {
                    new OpenApiString("year:from=2020"),
                    new OpenApiString("time:from=202*")
                }
            },
            ["to-filter"] = new OpenApiExample
            {
                Summary = "To filter",
                Description = "Years up to 2023; time codes up to first match starting with 2022.",
                Value = new OpenApiArray
                {
                    new OpenApiString("year:to=2023"),
                    new OpenApiString("time:to=2022*")
                }
            },
            ["first-filter"] = new OpenApiExample
            {
                Summary = "First filter",
                Description = "First 10 region codes.",
                Value = new OpenApiArray
                {
                    new OpenApiString("region:first=10")
                }
            },
            ["last-filter"] = new OpenApiExample
            {
                Summary = "Last filter",
                Description = "Last 5 region codes.",
                Value = new OpenApiArray
                {
                    new OpenApiString("region:last=5")
                }
            },
            ["combined-filters"] = new OpenApiExample
            {
                Summary = "Combined filters",
                Description = "Multiple types together.",
                Value = new OpenApiArray
                {
                    new OpenApiString("gender:code=1,2"),
                    new OpenApiString("year:from=2020"),
                    new OpenApiString("age:to=81-90"),
                    new OpenApiString("region:first=3"),
                    new OpenApiString("rooms:last=2")
                }
            }
        };
    }
}
