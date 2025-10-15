using Microsoft.OpenApi.Models;
using PxApi.Models.JsonStat;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PxApi.Configuration
{
    /// <summary>
    /// Adds the JsonStat2 schema to the OpenAPI document components by generating it from the model.
    /// </summary>
    public class JsonStat2ComponentDocumentFilter : IDocumentFilter
    {
        /// <summary>
        /// Applies the filter to add the JsonStat2 schema.
        /// </summary>
        /// <param name="swaggerDoc">The OpenAPI document.</param>
        /// <param name="context">The document filter context.</param>
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            // Ensure JsonStat2 schema is generated and present in the components
            if (!swaggerDoc.Components.Schemas.ContainsKey("JsonStat2"))
            {
                context.SchemaGenerator.GenerateSchema(typeof(JsonStat2), context.SchemaRepository);
            }
        }
    }
}
