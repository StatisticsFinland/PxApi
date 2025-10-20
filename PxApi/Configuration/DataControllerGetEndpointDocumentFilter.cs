using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PxApi.Configuration
{
    /// <summary>
    /// Document filter enhancing DataController GET endpoint documentation with examples and richer parameter descriptions.
    /// </summary>
    public class DataControllerGetEndpointDocumentFilter : IDocumentFilter
    {
        /// <summary>
        /// Applies enhancements to GET operations under /data.
        /// </summary>
        /// <param name="swaggerDoc">The OpenAPI document to modify.</param>
        /// <param name="context">Filter context.</param>
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            foreach (KeyValuePair<string, OpenApiPathItem> path in swaggerDoc.Paths)
            {
                if (path.Value.Operations.TryGetValue(OperationType.Get, out OpenApiOperation? getOp) && IsDataControllerGetOperation(path.Key, getOp))
                {
                    AddFiltersParameterDescription(getOp);
                    AddFiltersParameterExamples(getOp);
                    AddResponseExamples(getOp);
                    AppendAcceptHeaderNote(getOp);
                    ImproveLanguageParameter(getOp);
                }
            }
        }

        private static bool IsDataControllerGetOperation(string pathKey, OpenApiOperation operation)
        {
            bool isDataPath = pathKey.Equals("/data/{database}/{table}", StringComparison.OrdinalIgnoreCase);
            bool hasFiltersParam = operation.Parameters?.Any(p => p.Name == "filters") == true;
            return isDataPath && hasFiltersParam;
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

        private static void AddFiltersParameterDescription(OpenApiOperation operation)
        {
            OpenApiParameter? filtersParam = operation.Parameters?.FirstOrDefault(p => p.Name == "filters");
            if (filtersParam == null) return;
            filtersParam.Description =
                "Array of filter specs: 'dimension:filterType=value'. Types: code | from | to | first | last. Wildcard '*' matches zero or more characters. Single filter per dimension. first/last require integer > 0. from/to accept single value (wildcards allowed). code accepts one or more comma-separated values (wildcards allowed). Escaping '*' not supported; literal asterisk must be matched exactly if no wildcard semantics desired.";
        }

        private static void AddFiltersParameterExamples(OpenApiOperation operation)
        {
            OpenApiParameter? filtersParam = operation.Parameters?.FirstOrDefault(p => p.Name == "filters");
            if (filtersParam is null) return;
            filtersParam.Examples ??= new Dictionary<string, OpenApiExample>();
            filtersParam.Examples.Clear();

            Dictionary<string, OpenApiExample> filterExamples = new()
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
                    Value = new OpenApiArray { new OpenApiString("region:first=10") }
                },
                ["last-filter"] = new OpenApiExample
                {
                    Summary = "Last filter",
                    Description = "Last 5 region codes.",
                    Value = new OpenApiArray { new OpenApiString("region:last=5") }
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

            foreach (KeyValuePair<string, OpenApiExample> example in filterExamples)
            {
                filtersParam.Examples.Add(example.Key, example.Value);
            }
        }

        private static void AppendAcceptHeaderNote(OpenApiOperation operation)
        {
            operation.Description = (operation.Description ?? string.Empty) +
                "Accept header options: application/json (JSON-stat), text/csv (CSV), */* treated as JSON-stat. Unsupported media types yield 406.";
        }

        private static void ImproveLanguageParameter(OpenApiOperation operation)
        {
            OpenApiParameter? langParam = operation.Parameters?.FirstOrDefault(p => p.Name == "lang");
            if (langParam != null)
            {
                langParam.Description = "Optional language code (ISO 639-1). Defaults to table's default language. Must be one of the table's AvailableLanguages.";
            }
        }
    }
}