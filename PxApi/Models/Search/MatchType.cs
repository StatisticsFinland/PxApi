using System.Text.Json.Serialization;

namespace PxApi.Models.Search
{
    /// <summary>
    /// Describes how the search query matched the target text.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MatchType
    {
        /// <summary>
        /// The query text is contained within the matched field.
        /// </summary>
        Contains,

        /// <summary>
        /// The query text exactly matches the matched field.
        /// </summary>
        Exact
    }
}
