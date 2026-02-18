using Microsoft.OpenApi.Models;

namespace PxApi.OpenApi.DocumentFilters
{
    internal static class DocumentFilterUtilities
    {
        internal static void CleanUpErrorResponses(OpenApiOperation operation)
        {
            foreach (KeyValuePair<string, OpenApiResponse> response in operation.Responses)
            {
                if (response.Key == "200") continue;

                if (response.Key == "406")
                {
                    response.Value.Content.Clear();
                }
                else
                {
                    response.Value.Content.Remove("text/csv");
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
