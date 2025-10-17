using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PxApi.Configuration
{
    /// <summary>
    /// Adds the RootUrl from configuration as the single server entry in the OpenAPI document.
    /// </summary>
    public class RootUrlServersDocumentFilter : IDocumentFilter
    {
        /// <summary>
        /// Applies the server list modification.
        /// </summary>
        /// <param name="swaggerDoc">The OpenAPI document being built.</param>
        /// <param name="context">The document filter context.</param>
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            string rootUrl = AppSettings.Active.RootUrl.ToString().TrimEnd('/');
            swaggerDoc.Servers.Clear();
            if (!string.IsNullOrWhiteSpace(rootUrl))
            {
                swaggerDoc.Servers.Add(new OpenApiServer
                {
                    Url = rootUrl,
                    Description = "Primary API server"
                });
            }
        }
    }
}
