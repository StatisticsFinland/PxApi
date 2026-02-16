using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PxApi.Models.Search
{
    /// <summary>
    /// Represents a dimension search result item.
    /// </summary>
    public class DimensionSearchResult
    {
        /// <summary>
        /// Dimension identifier.
        /// </summary>
        [Required]
        public required string ID { get; set; }

        /// <summary>
        /// Dimension name.
        /// </summary>
        [Required]
        public required string Name { get; set; }

        /// <summary>
        /// Dimension classification/type.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Type { get; set; }

        /// <summary>
        /// Table identifiers where the dimension appears.
        /// When the table list is large, it may be truncated to a sample of table IDs.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? TableIds { get; set; }

        /// <summary>
        /// Matching values when searching by value.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SearchDimensionValueSummary>? MatchingValues { get; set; }

        /// <summary>
        /// Optional include payloads.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public SearchResultIncludes? Includes { get; set; }
    }
}
