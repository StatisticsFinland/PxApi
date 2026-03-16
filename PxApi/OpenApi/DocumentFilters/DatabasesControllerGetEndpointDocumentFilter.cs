using Microsoft.OpenApi;
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
            if (swaggerDoc.Paths == null)
            {
                return;
            }

            foreach (KeyValuePair<string, IOpenApiPathItem> path in swaggerDoc.Paths)
            {
                if (path.Key.Equals("/databases", StringComparison.OrdinalIgnoreCase) &&
                    path.Value is OpenApiPathItem pathItem &&
                    pathItem.Operations != null &&
                    pathItem.Operations.TryGetValue(HttpMethod.Get, out OpenApiOperation? getOp))
                {
                    AddResponseExample(getOp);
                }
            }
        }

        private static void AddResponseExample(OpenApiOperation operation)
        {
            if (operation.Responses == null ||
                !operation.Responses.TryGetValue("200", out IOpenApiResponse? response) ||
                response is not OpenApiResponse concreteResponse ||
                concreteResponse.Content == null ||
                !concreteResponse.Content.TryGetValue("application/json", out OpenApiMediaType? jsonMediaType))
            {
                return;
            }

            jsonMediaType.Examples = new Dictionary<string, IOpenApiExample>
            {
                ["default"] = new OpenApiExample { Value = DatabaseListingExample.Instance }
            };

            if (string.IsNullOrWhiteSpace(concreteResponse.Description))
            {
                concreteResponse.Description = "Returns list of available databases including translated name, optional translated description (nullable), tableCount, available languages and related links.";
            }
        }
    }
}
