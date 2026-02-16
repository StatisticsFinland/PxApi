using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PxApi.Models.Search
{
    /// <summary>
    /// Represents a dimension value search result item.
    /// </summary>
    public class ValueSearchResult
    {
        /// <summary>
        /// Value identifier.
        /// </summary>
        [Required]
        public required string ID { get; set; }

        /// <summary>
        /// Value label.
        /// </summary>
        [Required]
        public required string Name { get; set; }

        /// <summary>
        /// Database identifier.
        /// </summary>
        [Required]
        public required string Database { get; set; }

        /// <summary>
        /// Dimension information for the value.
        /// </summary>
        [Required]
        public required SearchDimensionSummary Dimension { get; set; }

        /// <summary>
        /// Table identifiers where the value appears.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? TableIds { get; set; }

        /// <summary>
        /// Optional include payloads.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public SearchResultIncludes? Includes { get; set; }
    }
}
