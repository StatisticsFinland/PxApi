using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace PxApi.OpenApi
{
    /// <summary>
    /// Operation filter that assigns explicit operationIds defined via <see cref="OperationIdAttribute"/>.
    /// </summary>
    public sealed class OperationIdOperationFilter : IOperationFilter
    {
        /// <summary>
        /// Applies the filter to the given operation.
        /// </summary>
        /// <param name="operation">The OpenAPI operation.</param>
        /// <param name="context">The current filter context.</param>
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            OperationIdAttribute? attr = context.MethodInfo.GetCustomAttribute<OperationIdAttribute>();
            if (attr != null && !string.IsNullOrWhiteSpace(attr.Id))
            {
                operation.OperationId = attr.Id;
            }
        }
    }
}
