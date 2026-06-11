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
                    "sukupuoli_9_20180101:code=1",
                    "ikaryhma_10_20180101:code=25-29,30-34",
                    "valtio_19_20190101:code=*",
                    "contentscode:code=*OSUUS*"
                )
            },
            ["from-filter"] = new OpenApiExample
            {
                Summary = "From filter",
                Description = "Months from 2020M01 onward; time codes starting with 202.",
                Value = new JsonArray(
                    "timeperiod_m:from=2020M01",
                    "timeperiod_y:from=202*"
                )
            },
            ["to-filter"] = new OpenApiExample
            {
                Summary = "To filter",
                Description = "Months up to 2023M12; time codes up to first match starting with 2022.",
                Value = new JsonArray(
                    "timeperiod_m:to=2023M12",
                    "timeperiod_y:to=2022*"
                )
            },
            ["first-filter"] = new OpenApiExample
            {
                Summary = "First filter",
                Description = "First 10 region codes.",
                Value = new JsonArray("valtio_19_20190101:first=10")
            },
            ["last-filter"] = new OpenApiExample
            {
                Summary = "Last filter",
                Description = "Last 5 region codes.",
                Value = new JsonArray("valtio_19_20190101:last=5")
            },
            ["combined-filters"] = new OpenApiExample
            {
                Summary = "Combined filters",
                Description = "Multiple types together.",
                Value = new JsonArray(
                    "sukupuoli_9_20180101:code=1,2",
                    "timeperiod_m:from=2020M01",
                    "ikaryhma_10_20180101:to=60-64",
                    "valtio_19_20190101:first=3",
                    "contentscode:last=2"
                )
            }
        };
    }
}
