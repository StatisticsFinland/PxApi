using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PxApi.OpenApi.DocumentFilters
{
    /// <summary>
    /// Ensures the OpenAPI document explicitly states that no authentication is required
    /// and removes any residual security schemes or global security requirements.
    /// Cache endpoints are excluded from the document via ApiExplorer conventions.
    /// </summary>
    public class NoAuthDocumentFilter : IDocumentFilter
    {
        /// <summary>
        /// Applies modifications to remove security schemes and add a descriptive note.
        /// </summary>
        /// <param name="swaggerDoc">The OpenAPI document.</param>
        /// <param name="context">The document filter context.</param>
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            // Remove any security schemes just in case (should normally be empty)
            swaggerDoc.Components.SecuritySchemes.Clear();
            // Clear global security requirements
            swaggerDoc.SecurityRequirements.Clear();

            // Append note to the top-level description if not already appended
            const string noAuthNote = "Authentication: This public API requires no authentication.";
            if (swaggerDoc.Info.Description is null || !swaggerDoc.Info.Description.Contains("requires no authentication"))
            {
                swaggerDoc.Info.Description = (swaggerDoc.Info.Description ?? string.Empty) + noAuthNote;
            }
        }
    }
}
