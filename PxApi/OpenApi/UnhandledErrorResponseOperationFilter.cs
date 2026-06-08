using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PxApi.OpenApi
{
    /// <summary>
    /// Adds a generic 500 response to all operations if not already defined.
    /// </summary>
    public class UnhandledErrorResponseOperationFilter : IOperationFilter
    {
        /// <summary>
        /// Applies the filter to add a 500 string response if absent.
        /// </summary>
        /// <param name="operation">The OpenAPI operation.</param>
        /// <param name="context">The operation filter context.</param>
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (operation.Responses == null || operation.Responses.ContainsKey("500"))
            {
                return;
            }

            operation.Responses["500"] = new OpenApiResponse
            {
                Description = "Unexpected server error.",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                    }
                }
            };
        }
    }
}
