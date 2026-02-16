using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PxApi.Models.Search
{
    /// <summary>
    /// Represents a dimension summary in search results.
    /// </summary>
    public class SearchDimensionSummary
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
        /// Optional dimension type/classification.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Type { get; set; }
    }
}
