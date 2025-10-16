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
        public required List<string> Index { get; init; }

        /// <summary>
        /// The labels for each category value.
        /// </summary>
        [Required]
        [JsonPropertyName("label")]
        public required Dictionary<string, string> Label { get; init; }

        /// <summary>
        /// Possible annotations related to some category values.
        /// </summary>
        [JsonPropertyName("note")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, string[]>? Note { get; set; } // All dimensions do not have value notes, therefore no init, but set.

        /// <summary>
        /// The unit of measurement for each category.
        /// </summary>
        [JsonPropertyName("unit")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, Unit>? Unit { get; set; } // Unit is only for content dimension values, therefore no init but set.
    }
}
