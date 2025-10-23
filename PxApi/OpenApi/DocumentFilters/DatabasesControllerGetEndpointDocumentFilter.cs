using Microsoft.OpenApi.Models;
using PxApi.OpenApi.Examples;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Diagnostics.CodeAnalysis;

namespace PxApi.OpenApi.DocumentFilters
{
    /// <summary>
    /// Adds example response documentation for the DatabasesController GET /databases endpoint.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class DatabasesControllerGetEndpointDocumentFilter : IDocumentFilter
    {
        /// <summary>
        /// Applies documentation enhancements to the /databases GET operation by injecting an example response.
        /// </summary>
        /// <param name="swaggerDoc">OpenAPI document.</param>
        /// <param name="context">Filter context.</param>
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            foreach (KeyValuePair<string, OpenApiPathItem> path in swaggerDoc.Paths)
            {
                if (path.Key.Equals("/databases", StringComparison.OrdinalIgnoreCase) &&
                path.Value.Operations.TryGetValue(OperationType.Get, out OpenApiOperation? getOp))
                {
                    AddResponseExample(getOp);
                }
            }
        }

        private static void AddResponseExample(OpenApiOperation operation)
        {
            if (!operation.Responses.TryGetValue("200", out OpenApiResponse? response)) return;
            if (!response.Content.TryGetValue("application/json", out OpenApiMediaType? jsonMediaType)) return;

            jsonMediaType.Example = DatabaseListingExample.Instance;
            if (string.IsNullOrWhiteSpace(response.Description))
            {
                response.Description = "Returns list of available databases including translated name, optional translated description (nullable), tableCount, available languages and related links.";
            }
        }
    }
}
