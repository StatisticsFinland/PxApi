using Microsoft.OpenApi;

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
    }
}
