using System.Text.Json.Serialization;

namespace PxApi.Models.Search
{
    /// <summary>
    /// Optional include payloads for a search result.
    /// </summary>
    public class SearchResultIncludes
    {
        /// <summary>
        /// Highlighted fragments by field.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, List<string>>? Highlights { get; set; }

        /// <summary>
        /// Snippet fragments by field.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, List<string>>? Snippets { get; set; }

        /// <summary>
        /// List of matched fields.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Match { get; set; }
    }
}
