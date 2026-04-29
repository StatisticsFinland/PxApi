using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PxApi.OpenApi
{
    /// <summary>
    /// Adds a 499 (Client Closed Request) response to all operations whose action method
    /// accepts a <see cref="CancellationToken"/> parameter, indicating the client may
    /// disconnect before the server finishes processing.
    /// </summary>
    public class ClientClosedRequestOperationFilter : IOperationFilter
    {
        /// <inheritdoc />
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (operation.Responses == null || operation.Responses.ContainsKey("499"))
            {
                return;
            }

            bool acceptsCancellationToken = context.MethodInfo
                .GetParameters()
                .Any(p => p.ParameterType == typeof(CancellationToken));

            if (acceptsCancellationToken)
            {
                operation.Responses["499"] = new OpenApiResponse
                {
                    Description = "Client closed the request before the server could respond."
                };
            }
        }
    }
}
