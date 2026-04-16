using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PxApi.Models.Search
{
    /// <summary>
    /// A single search result representing a table. Each table appears at most once.
    /// </summary>
    public class SearchResultItem
    {
        /// <summary>
        /// Optional numeric relevance score used for ordering results.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Score { get; set; }

        /// <summary>
        /// Reference to the database this result belongs to. Always present.
        /// </summary>
        [Required]
        public required SearchEntityRef Database { get; set; }

        /// <summary>
        /// Reference to the table this result belongs to. Always present.
        /// </summary>
        [Required]
        public required SearchEntityRef Table { get; set; }

        /// <summary>
        /// Describes where and how the search hit occurred.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<MatchInfo>? Matches { get; set; }

        /// <summary>
        /// HATEOAS-style links to related resources.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<Link>? Links { get; set; }
    }
}
