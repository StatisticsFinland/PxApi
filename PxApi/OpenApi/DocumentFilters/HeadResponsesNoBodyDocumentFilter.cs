using Microsoft.OpenApi.Models;
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
            foreach (KeyValuePair<string, OpenApiPathItem> path in swaggerDoc.Paths)
            {
                if (path.Value.Operations.TryGetValue(OperationType.Head, out OpenApiOperation? headOp))
                {
                    foreach (KeyValuePair<string, OpenApiResponse> responsePair in headOp.Responses)
                    {
                        // Clear any defined content (schemas/examples) so tools do not expect a body.
                        responsePair.Value.Content?.Clear();
                    }
                }
            }
        }
    }
}
