using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PxApi.Models.JsonStat
{
    /// <summary>
    /// Represents a category in a dimension.
    /// </summary>
    public class Category
    {
        /// <summary>
        /// The index array of the category.
        /// </summary>
        [Required]
        [JsonPropertyName("index")]
        public required List<string> Index { get; set; }

        /// <summary>
        /// The labels for each category value.
        /// </summary>
        [Required]
        [JsonPropertyName("label")]
        public required Dictionary<string, string> Label { get; set; }

        /// <summary>
        /// The unit of measurement for each category.
        /// </summary>
        [JsonPropertyName("unit")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, Unit>? Unit { get; set; }
    }
}
