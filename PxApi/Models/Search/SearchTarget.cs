using System.Text.Json.Serialization;

namespace PxApi.Models.Search
{
    /// <summary>
    /// Specifies which fields the search engine should match against.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SearchTarget
    {
        /// <summary>
        /// Search table-level content fields: title, source, note, content variable, and used-for description.
        /// </summary>
        Content,

        /// <summary>
        /// Search classificatory variable names.
        /// </summary>
        Dimension,

        /// <summary>
        /// Search classificatory variable values.
        /// </summary>
        Value,

        /// <summary>
        /// Search geographic variable values.
        /// </summary>
        Geo,

        /// <summary>
        /// Search all fields.
        /// </summary>
        All
    }
}
