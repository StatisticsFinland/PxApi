using System.Text.Json.Serialization;

namespace PxApi.Models.Search
{
    /// <summary>
    /// Identifies the kind of entity a search result represents.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SearchResultType
    {
        /// <summary>
        /// The result is a table (cube).
        /// </summary>
        Table,

        /// <summary>
        /// The result is a dimension within a table.
        /// </summary>
        Dimension,

        /// <summary>
        /// The result is a value within a dimension.
        /// </summary>
        Value
    }
}
