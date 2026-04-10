using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PxApi.Models.Search
{
    /// <summary>
    /// A single search result item. The set of populated nested objects depends on <see cref="Type"/>.
    /// </summary>
    public class SearchResultItem
    {
        /// <summary>
        /// The kind of entity this result represents (table, dimension, or value).
        /// </summary>
        [Required]
        public required SearchResultType Type { get; set; }

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
        /// Reference to the dimension. Required for dimension and value results; null for table results.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DimensionRef? Dimension { get; set; }

        /// <summary>
        /// Reference to the value. Required for value results; null for table and dimension results.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public SearchEntityRef? Value { get; set; }

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
