using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PxApi.Configuration
{
    /// <summary>
    /// Document filter to enhance DataController GET endpoint documentation with proper query parameter examples.
    /// This filter now works alongside DynamicQueryParameterOperationFilter to provide additional examples
    /// for the structured filters parameter.
    /// </summary>
    public class DataControllerGetEndpointDocumentFilter : IDocumentFilter
    {
        // Parameter descriptions for OpenAPI documentation
        private const string DatabaseParameterDescription = "Name of the database containing the table";
        private const string TableParameterDescription = "Name of the px table to query";
        private const string FiltersParameterDescription = "Array of filter specifications in the format 'dimension:filterType=value'. Supported filter formats: dimension:code=value1,value2,value3 - Creates a CodeFilter with multiple values, dimension:code=* - Creates a CodeFilter that matches all values (wildcard), dimension:from=valueX - Creates a FromFilter starting from valueX, dimension:to=valueX - Creates a ToFilter up to valueX, dimension:first=N - Creates a FirstFilter that takes the first N values, dimension:last=N - Creates a LastFilter that takes the last N values. Example: ?filters[]=gender:code=1,2&filters[]=year:from=2020&filters[]=region:last=5 This will filter the gender dimension to codes 1 and 2, the year dimension from 2020 onwards, and return only the last 5 regions.";
        private const string LanguageParameterDescription = "Language code for the response. If not provided, uses the default language of the table";

        /// <summary>
        /// Applies enhanced documentation to DataController GET endpoints with query parameters.
        /// </summary>
        /// <param name="swaggerDoc">The OpenAPI document to modify.</param>
        /// <param name="context">The document filter context.</param>
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            foreach (KeyValuePair<string, OpenApiPathItem> path in swaggerDoc.Paths)
            {
                if (path.Value.Operations.TryGetValue(OperationType.Get, out OpenApiOperation? getOp))
                {
                    // Check if this is a DataController GET operation with the new filters parameter
                    if (IsDataControllerGetOperation(path.Key, getOp))
                    {
                        AddFiltersParameterExamples(getOp);
                        AddParameterDescriptions(getOp);
                    }
                }
            }
        }

        /// <summary>
        /// Determines if this is a DataController GET operation that accepts filter parameters.
        /// </summary>
        /// <param name="pathKey">The path key from the OpenAPI document.</param>
        /// <param name="operation">The GET operation to check.</param>
        /// <returns>True if this is a DataController GET operation with filter parameters.</returns>
        private static bool IsDataControllerGetOperation(string pathKey, OpenApiOperation operation)
        {
            // Check if the path matches DataController GET endpoints
            bool hasJsonPath = pathKey.Contains("/json/") || pathKey.Contains("/json-stat");
            bool hasFiltersParam = operation.Parameters?.Any(p => p.Name == "filters") == true;
            
            return hasJsonPath && hasFiltersParam;
        }

        /// <summary>
        /// Adds additional comprehensive examples to the filters parameter.
        /// </summary>
        /// <param name="operation">The GET operation to enhance with examples.</param>
        private static void AddFiltersParameterExamples(OpenApiOperation operation)
        {
            // Find the filters parameter
            OpenApiParameter? filtersParam = operation.Parameters?.FirstOrDefault(p => p.Name == "filters");
            if (filtersParam?.Examples == null) return;

            // Add more real-world examples to complement the ones from DynamicQueryParameterOperationFilter
            Dictionary<string, OpenApiExample> additionalExamples = new()
            {
                ["demographic-analysis"] = new OpenApiExample
                {
                    Summary = "Demographic analysis",
                    Description = "Filter for demographic analysis with specific age groups and recent years",
                    Value = new OpenApiArray
                    {
                        new OpenApiString("age:code=25-34,35-44"),
                        new OpenApiString("gender:code=1,2"),
                        new OpenApiString("year:last=3")
                    }
                },
                ["regional-comparison"] = new OpenApiExample
                {
                    Summary = "Regional comparison",
                    Description = "Compare regions for a specific time period",
                    Value = new OpenApiArray
                    {
                        new OpenApiString("region:first=10"),
                        new OpenApiString("year:from=2020"),
                        new OpenApiString("year:to=2023")
                    }
                },
                ["time-series"] = new OpenApiExample
                {
                    Summary = "Time series data",
                    Description = "Get all data for specific dimensions over time",
                    Value = new OpenApiArray
                    {
                        new OpenApiString("region:code=Helsinki,Tampere,Turku"),
                        new OpenApiString("gender:code=*"),
                        new OpenApiString("year:from=2015")
                    }
                },
                ["category-breakdown"] = new OpenApiExample
                {
                    Summary = "Category breakdown",
                    Description = "Detailed breakdown by categories with wildcards",
                    Value = new OpenApiArray
                    {
                        new OpenApiString("category:code=*manufacturing*"),
                        new OpenApiString("region:last=5"),
                        new OpenApiString("year:code=2022,2023")
                    }
                }
            };

            // Add the additional examples to the existing ones
            foreach (KeyValuePair<string, OpenApiExample> example in additionalExamples)
            {
                if (!filtersParam.Examples.ContainsKey(example.Key))
                {
                    filtersParam.Examples.Add(example.Key, example.Value);
                }
            }
        }

        /// <summary>
        /// Adds parameter descriptions to GET operations.
        /// </summary>
        /// <param name="operation">The GET operation to enhance with parameter descriptions.</param>
        private static void AddParameterDescriptions(OpenApiOperation operation)
        {
            if (operation.Parameters == null) return;

            foreach (OpenApiParameter parameter in operation.Parameters)
            {
                if (string.IsNullOrEmpty(parameter.Description))
                {
                    parameter.Description = parameter.Name.ToLowerInvariant() switch
                    {
                        "database" => DatabaseParameterDescription,
                        "table" => TableParameterDescription,
                        "filters" => FiltersParameterDescription,
                        "lang" => LanguageParameterDescription,
                        _ => parameter.Description
                    };
                }
            }
        }
    }
}