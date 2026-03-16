using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Diagnostics.CodeAnalysis;

namespace PxApi.OpenApi.DocumentFilters
{
    /// <summary>
    /// Removes bodies (content schemas/examples) from all HEAD operation responses.
    /// HEAD responses must not include a payload; only headers and status codes.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class HeadResponsesNoBodyDocumentFilter : IDocumentFilter
    {
        /// <summary>
        /// Applies the filter by clearing the <see cref="OpenApiResponse.Content"/> for HEAD operations.
        /// </summary>
        /// <param name="swaggerDoc">The OpenAPI document.</param>
        /// <param name="context">The document filter context.</param>
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            if (swaggerDoc.Paths == null)
            {
                return;
            }

            foreach (KeyValuePair<string, IOpenApiPathItem> path in swaggerDoc.Paths)
            {
                if (path.Value is OpenApiPathItem pathItem && 
                    pathItem.Operations != null &&
                    pathItem.Operations.TryGetValue(HttpMethod.Head, out OpenApiOperation? headOp) &&
                    headOp.Responses != null)
                {
                    foreach (KeyValuePair<string, IOpenApiResponse> responsePair in headOp.Responses)
                    {
                        if (responsePair.Value is OpenApiResponse response)
                        {
                            // Clear any defined content (schemas/examples) so tools do not expect a body.
                            response.Content?.Clear();
                        }
                    }
                }
            }
        }
    }
}
