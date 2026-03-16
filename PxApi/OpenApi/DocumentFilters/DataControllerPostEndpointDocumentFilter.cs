using Microsoft.OpenApi;
using PxApi.OpenApi.Examples;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PxApi.OpenApi.DocumentFilters
{
    /// <summary>
    /// Enhances DataController POST endpoint documentation with request body examples and refined descriptions.
    /// </summary>
    public class DataControllerPostEndpointDocumentFilter : IDocumentFilter
    {
        /// <summary>
        /// Applies documentation enhancements to POST operations under /data.
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
                if (path.Value is OpenApiPathItem pathItem &&
                    pathItem.Operations != null &&
                    pathItem.Operations.TryGetValue(HttpMethod.Post, out OpenApiOperation? postOp) &&
                    IsDataControllerPostOperation(path.Key, postOp))
                {
                    AddComprehensiveRequestBodyExamples(postOp);
                    DocumentFilterUtilities.AddResponseExamples(postOp);
                    RefineLanguageParameter(postOp);
                    DocumentFilterUtilities.AppendAcceptHeaderNote(postOp);
                    DocumentFilterUtilities.CleanUpErrorResponses(postOp);
                }
            }

            if (swaggerDoc.Components?.Schemas != null &&
                swaggerDoc.Components.Schemas.TryGetValue("Filter", out IOpenApiSchema? filterSchema) &&
                filterSchema is OpenApiSchema concreteFilterSchema &&
                string.IsNullOrWhiteSpace(concreteFilterSchema.Description))
            {
                concreteFilterSchema.Description = "Filter object. type determines behavior (Code | From | To | First | Last). query is array[string] (Code), string (From/To), integer>0 (First/Last). '*' wildcard matches zero or more characters.";
            }
        }

        private static void AddComprehensiveRequestBodyExamples(OpenApiOperation operation)
        {
            if (operation.RequestBody is not OpenApiRequestBody requestBody ||
                requestBody.Content == null)
            {
                return;
            }

            // Use external examples provider
            IReadOnlyDictionary<string, OpenApiExample> examples = DataRequestBodyExamples.Examples;
            foreach (OpenApiMediaType mediaType in requestBody.Content.Values)
            {
                mediaType.Examples = new Dictionary<string, IOpenApiExample>(examples.Select(kvp => 
                    new KeyValuePair<string, IOpenApiExample>(kvp.Key, kvp.Value)));

                if (mediaType.Schema is OpenApiSchema schema &&
                    string.IsNullOrWhiteSpace(schema.Description))
                {
                    schema.Description = "Dictionary mapping dimension codes to filter objects (one per dimension).";
                }
            }
        }

        private static bool IsDataControllerPostOperation(string pathKey, OpenApiOperation operation)
        {
            bool isDataPath = pathKey.Equals("/data/{database}/{table}", StringComparison.OrdinalIgnoreCase);
            bool hasRequestBody = operation.RequestBody is OpenApiRequestBody rb && rb.Content?.Any() == true;
            return isDataPath && hasRequestBody;
        }

        private static void RefineLanguageParameter(OpenApiOperation operation)
        {
            IOpenApiParameter? langParam = operation.Parameters?.FirstOrDefault(p => p.Name == "lang");
            if (langParam is OpenApiParameter concreteParam)
            {
                concreteParam.Description = "Optional language code (ISO639-1). Defaults to table's default language. Must be one of the table's AvailableLanguages.";
            }
        }
    }
}