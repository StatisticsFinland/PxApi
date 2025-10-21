using Microsoft.OpenApi.Models;
using PxApi.OpenApi.Examples;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PxApi.OpenApi.DocumentFilters
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
                        response.Description = "Returns JSON-stat2.0 dataset when 'Accept: application/json' or '*/*'. Use 'Accept: text/csv' for CSV output.";
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

            // Use external examples provider
            IReadOnlyDictionary<string, OpenApiExample> examples = DataRequestBodyExamples.Examples;
            foreach (OpenApiMediaType mediaType in requestBody.Content.Values)
            {
                mediaType.Examples = new Dictionary<string, OpenApiExample>(examples);
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
                langParam.Description = "Optional language code (ISO639-1). Defaults to table's default language. Must be one of the table's AvailableLanguages.";
            }
        }

        private static void AppendAcceptHeaderNote(OpenApiOperation operation)
        {
            operation.Description = (operation.Description ?? string.Empty) +
                "Accept header options: application/json (JSON-stat), text/csv (CSV), */* treated as JSON-stat. Unsupported media types yield406.";
        }
    }
}