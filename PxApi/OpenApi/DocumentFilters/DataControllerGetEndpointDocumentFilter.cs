using Microsoft.OpenApi;
using PxApi.OpenApi.Examples;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PxApi.OpenApi.DocumentFilters
{
    /// <summary>
    /// Document filter enhancing DataController GET endpoint documentation with examples and richer parameter descriptions.
    /// </summary>
    public class DataControllerGetEndpointDocumentFilter : IDocumentFilter
    {
        /// <summary>
        /// Applies enhancements to GET operations under /data.
        /// </summary>
        /// <param name="swaggerDoc">The OpenAPI document to modify.</param>
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
                    pathItem.Operations.TryGetValue(HttpMethod.Get, out OpenApiOperation? getOp) &&
                    IsDataControllerGetOperation(path.Key, getOp))
                {
                    AddFiltersParameterDescription(getOp);
                    AddFiltersParameterExamples(getOp);
                    DocumentFilterUtilities.AddResponseExamples(getOp);
                    DocumentFilterUtilities.AppendAcceptHeaderNote(getOp);
                    ImproveLanguageParameter(getOp);
                    DocumentFilterUtilities.CleanUpErrorResponses(getOp);
                }
            }
        }

        private static bool IsDataControllerGetOperation(string pathKey, OpenApiOperation operation)
        {
            bool isDataPath = pathKey.Equals("/data/{database}/{table}", StringComparison.OrdinalIgnoreCase);
            bool hasFiltersParam = operation.Parameters?.Any(p => p.Name == "filters") == true;
            return isDataPath && hasFiltersParam;
        }

        private static void AddFiltersParameterDescription(OpenApiOperation operation)
        {
            IOpenApiParameter? filtersParam = operation.Parameters?.FirstOrDefault(p => p.Name == "filters");
            if (filtersParam is not OpenApiParameter concreteParam)
            {
                return;
            }

            concreteParam.Description =
                "Array of filter specs: 'dimension:filterType=value'. Types: code | from | to | first | last. Wildcard '*' matches zero or more characters. Single filter per dimension. first/last require integer > 0. from/to accept single value (wildcards allowed). code accepts one or more comma-separated values (wildcards allowed). Escaping '*' not supported; literal asterisk must be matched exactly if no wildcard semantics desired.";
        }

        private static void AddFiltersParameterExamples(OpenApiOperation operation)
        {
            IOpenApiParameter? filtersParam = operation.Parameters?.FirstOrDefault(p => p.Name == "filters");
            if (filtersParam is not OpenApiParameter concreteParam)
            {
                return;
            }

            concreteParam.Examples ??= new Dictionary<string, IOpenApiExample>();
            concreteParam.Examples.Clear();

            foreach (KeyValuePair<string, OpenApiExample> example in FiltersParameterExamples.Examples)
            {
                concreteParam.Examples.Add(example.Key, example.Value);
            }
        }

        private static void ImproveLanguageParameter(OpenApiOperation operation)
        {
            IOpenApiParameter? langParam = operation.Parameters?.FirstOrDefault(p => p.Name == "lang");
            if (langParam is OpenApiParameter concreteParam)
            {
                concreteParam.Description = "Optional language code (ISO 639-1). Defaults to table's default language. Must be one of the table's AvailableLanguages.";
            }
        }
    }
}