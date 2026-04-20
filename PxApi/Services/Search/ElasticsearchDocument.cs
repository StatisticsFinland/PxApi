using System.Text.Json.Serialization;

namespace PxApi.Services.Search
{
    /// <summary>
    /// Maps the fields returned from Elasticsearch <c>_source</c>.
    /// Only the fields needed for building the API response are included;
    /// other index fields are used only for querying and highlighting.
    /// </summary>
    public class ElasticsearchDocument
    {
        /// <summary>
        /// Database identifier this table belongs to.
        /// </summary>
        [JsonPropertyName("database")]
        public string Database { get; init; } = string.Empty;

        /// <summary>
        /// Localized table title.
        /// </summary>
        [JsonPropertyName("title")]
        public string Title { get; init; } = string.Empty;

        /// <summary>
        /// Optional note or description for the table.
        /// </summary>
        [JsonPropertyName("note")]
        public string? Note { get; init; }
    }
}
