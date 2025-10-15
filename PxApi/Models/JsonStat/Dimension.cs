using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PxApi.Models.JsonStat
{
    /// <summary>
    /// Represents a dimension in the JSON-stat format.
    /// </summary>
    public class Dimension
    {
        /// <summary>
        /// The label of the dimension.
        /// </summary>
        [Required]
        [JsonPropertyName("label")]
        public required string Label { get; set; }

        /// <summary>
        /// Annotations about the dimension. Language-dependent.
        /// </summary>
        [JsonPropertyName("note")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string[]? Note { get; init; }

        /// <summary>
        /// The category object containing dimension categories.
        /// </summary>
        [Required]
        [JsonPropertyName("category")]
        public required Category Category { get; set; }
    }
}
