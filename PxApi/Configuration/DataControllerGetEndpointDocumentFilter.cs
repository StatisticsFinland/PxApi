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
        private const string FiltersParameterDescription = "Array of filter specifications in the format 'dimension:filterType=value'. Supported filter formats: dimension:code=value1,value2,value3 - Creates a CodeFilter with multiple values, dimension:code=* - Creates a CodeFilter that matches all values (wildcard), dimension:from=valueX - Creates a FromFilter starting from valueX, dimension:to=valueX - Creates a ToFilter up to valueX, dimension:first=N - Creates a FirstFilter that takes the first N values, dimension:last=N - Creates a LastFilter that takes the last N values. Wildcards (*) are supported for 'code', 'from', and 'to' filters, and can be partial (e.g., '202*'). Example: ?filters[]=gender:code=1,2&filters[]=year:from=2020&filters[]=region:last=5 This will filter the gender dimension to codes 1 and 2, the year dimension from 2020 onwards, and return only the last 5 regions.";
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
                        AddResponseExamples(getOp);
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
            bool isDataPath = pathKey.Equals("/data/{database}/{table}", StringComparison.OrdinalIgnoreCase);
            bool hasFiltersParam = operation.Parameters?.Any(p => p.Name == "filters") == true;
            
            return isDataPath && hasFiltersParam;
        }

        /// <summary>
        /// Adds example of a JsonStat2 response to the operation's documentation.
        /// </summary>
        /// <param name="operation">The GET operation to enhance with response examples.</param>
        private static void AddResponseExamples(OpenApiOperation operation)
        {
            if (operation.Responses.TryGetValue("200", out OpenApiResponse? response) &&
                response.Content.TryGetValue("application/json", out OpenApiMediaType? mediaType))
            {
                mediaType.Schema = new OpenApiSchema
                {
                    Reference = new OpenApiReference
                    {
                        Id = "JsonStat2",
                        Type = ReferenceType.Schema
                    }
                };
                mediaType.Example = JsonStat2Example.Instance;
            }
        }

        /// <summary>
        /// Adds additional comprehensive examples to the filters parameter.
        /// </summary>
        /// <param name="operation">The GET operation to enhance with examples.</param>
        private static void AddFiltersParameterExamples(OpenApiOperation operation)
        {
            // Find the filters parameter
            OpenApiParameter? filtersParam = operation.Parameters?.FirstOrDefault(p => p.Name == "filters");
            if (filtersParam is null) return;

            // Ensure the Examples dictionary exists
            filtersParam.Examples ??= new Dictionary<string, OpenApiExample>();

            // Clear existing examples to replace them
            filtersParam.Examples.Clear();

            // Add examples for each filter type
            Dictionary<string, OpenApiExample> filterExamples = new()
            {
                // CodeFilter examples
                ["code-filter"] = new OpenApiExample
                {
                    Summary = "Code filter",
                    Description = "Examples of filtering by code. This query filters by a single gender, multiple age groups, all regions (full wildcard), and categories containing 'manufacturing' (partial wildcard).",
                    Value = new OpenApiArray
                    {
                        new OpenApiString("gender:code=1"),
                        new OpenApiString("age:code=25-34,35-44"),
                        new OpenApiString("region:code=*"),
                        new OpenApiString("category:code=*manufacturing*")
                    }
                },

                // FromFilter examples
                ["from-filter"] = new OpenApiExample
                {
                    Summary = "From filter",
                    Description = "Examples of filtering from a specific starting point, including with wildcards. This query selects years from 2020 onwards and time periods from the first one starting with '202'.",
                    Value = new OpenApiArray
                    {
                        new OpenApiString("year:from=2020"),
                        new OpenApiString("time:from=202*")
                    }
                },

                // ToFilter examples
                ["to-filter"] = new OpenApiExample
                {
                    Summary = "To filter",
                    Description = "Examples of filtering up to a specific ending point, including with wildcards. This query selects years up to 2023 and time periods up to the last one starting with '2022'.",
                    Value = new OpenApiArray
                    {
                        new OpenApiString("year:to=2023"),
                        new OpenApiString("time:to=2022*")
                    }
                },

                // FirstFilter example
                ["first-filter"] = new OpenApiExample
                {
                    Summary = "First filter",
                    Description = "Select the first N values of a dimension.",
                    Value = new OpenApiArray { new OpenApiString("region:first=10") }
                },

                // LastFilter example
                ["last-filter"] = new OpenApiExample
                {
                    Summary = "Last filter",
                    Description = "Select the last N values of a dimension.",
                    Value = new OpenApiArray { new OpenApiString("region:last=5") }
                }
            };

            // Add the new examples
            foreach (KeyValuePair<string, OpenApiExample> example in filterExamples)
            {
                filtersParam.Examples.Add(example.Key, example.Value);
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