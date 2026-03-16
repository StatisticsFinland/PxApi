using PxApi.Configuration;
using PxApi.Utilities;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;

namespace PxApi.OpenApi.Examples
{
    /// <summary>
    /// Provides an example array response for the GET /databases endpoint.
    /// </summary>
    public static class DatabaseListingExample
    {
        private static string TablesHrefExample => AppSettings.Active.RootUrl
            .AddRelativePath("tables", "StatFin")
            .AddQueryParameters(("lang", "fi"))
            .ToString();

        /// <summary>
        /// Gets the singleton instance of the databases listing example.
        /// The RootUrl from configuration is used to build the link href dynamically.
        /// </summary>
        [SuppressMessage("SonarAnalyzer.CSharp", "S1192", Justification = "Duplicate string literals are intentional to represent example JSON structure.")]
        public static JsonNode Instance => new JsonArray(
            new JsonObject
            {
                ["id"] = "StatFin",
                ["name"] = "StatFin",
                ["description"] = null,
                ["tableCount"] = 1526,
                ["availableLanguages"] = new JsonArray("fi", "sv", "en"),
                ["links"] = new JsonArray(
                    new JsonObject
                    {
                        ["href"] = TablesHrefExample,
                        ["rel"] = "describedby",
                        ["method"] = "GET"
                    }
                )
            }
        );
    }
}