using System.Text.Json.Serialization;

namespace PxApi.Models.Search
{
    /// <summary>
    /// Defines supported sort orders for search results.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SearchSortOrder
    {
        /// <summary>
        /// Sort results by relevance score.
        /// </summary>
        Relevance,

        /// <summary>
        /// Sort results by name.
        /// </summary>
        Name
    }
}
