using Microsoft.OpenApi.Any;
using PxApi.Configuration;
using PxApi.Utilities;
using System.Diagnostics.CodeAnalysis;

namespace PxApi.OpenApi.Examples
{
    /// <summary>
    /// Provides an example array response for the GET /databases endpoint.
    /// </summary>
    public static class DatabaseListingExample
    {
        private static readonly string TablesHrefExample = AppSettings.Active.RootUrl
                .AddRelativePath("tables", "StatFin")
                .AddQueryParameters(("lang", "fi"))
                .ToString();

        /// <summary>
        /// Gets the singleton instance of the databases listing example.
        /// The RootUrl from configuration is used to build the link href dynamically.
        /// </summary>
        [SuppressMessage("SonarAnalyzer.CSharp", "S1192", Justification = "Duplicate string literals are intentional to represent example JSON structure.")]
        public static IOpenApiAny Instance => new OpenApiArray
        {
            new OpenApiObject
            {
                ["id"] = new OpenApiString("StatFin"),
                ["name"] = new OpenApiString("StatFin"),
                ["description"] = new OpenApiNull(),
                ["tableCount"] = new OpenApiInteger(1526),
                ["availableLanguages"] = new OpenApiArray
                {
                    new OpenApiString("fi"),
                    new OpenApiString("sv"),
                    new OpenApiString("en")
                },
                ["links"] = new OpenApiArray
                {
                    new OpenApiObject
                    {
                        ["href"] = new OpenApiString(TablesHrefExample),
                        ["rel"] = new OpenApiString("describedby"),
                        ["method"] = new OpenApiString("GET")
                    }
                }
            }
        };
    }
}
