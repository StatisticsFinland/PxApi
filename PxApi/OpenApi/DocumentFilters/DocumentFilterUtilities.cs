using Microsoft.OpenApi;
using PxApi.OpenApi.Examples;

namespace PxApi.OpenApi.DocumentFilters
{
    internal static class DocumentFilterUtilities
    {
        internal static void CleanUpErrorResponses(OpenApiOperation operation)
        {
            if (operation.Responses == null)
            {
                return;
            }

            foreach (KeyValuePair<string, IOpenApiResponse> response in operation.Responses)
            {
                if (response.Key == "200" || response.Value is not OpenApiResponse concreteResponse)
                {
                    continue;
                }

                if (response.Key == "406")
                {
                    concreteResponse.Content?.Clear();
                }
                else
                {
                    concreteResponse.Content?.Remove("text/csv");
                }
            }
        }

        internal static void AppendAcceptHeaderNote(OpenApiOperation operation)
        {
            operation.Description = (operation.Description ?? string.Empty) +
                " Accept header options: application/json (JSON-stat), text/csv (CSV), */* treated as JSON-stat. Unsupported media types yield 406.";
        }

        internal static void AddResponseExamples(OpenApiOperation operation)
        {
            if (operation.Responses == null ||
                !operation.Responses.TryGetValue("200", out IOpenApiResponse? response) ||
                response is not OpenApiResponse concreteResponse ||
                concreteResponse.Content == null)
            {
                return;
            }

            if (concreteResponse.Content.TryGetValue("application/json", out OpenApiMediaType? jsonMediaType))
            {
                jsonMediaType.Schema = new OpenApiSchemaReference("JsonStat2");
                jsonMediaType.Examples = new Dictionary<string, IOpenApiExample>
                {
                    ["default"] = new OpenApiExample { Value = JsonStat2Example.Instance }
                };
                if (string.IsNullOrWhiteSpace(concreteResponse.Description))
                {
                    concreteResponse.Description = "Returns JSON-stat 2.0 dataset when 'Accept: application/json' or '*/*'. Use 'Accept: text/csv' for CSV output.";
                }
            }

            if (concreteResponse.Content.TryGetValue("text/csv", out OpenApiMediaType? csvMediaType) &&
                csvMediaType.Schema is OpenApiSchema csvSchema &&
                string.IsNullOrWhiteSpace(csvSchema.Description))
            {
                csvSchema.Description = "CSV dataset (UTF-8, comma separated, header row). Column order follows dimension order then metric.";
            }
        }
    }
}
