using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PxApi.Models.JsonStat
{
    /// <summary>
    /// Represents a unit of measurement in the JSON-stat format.
    /// </summary>
    public class Unit
    {
        /// <summary>
        /// Localized label of the unit (e.g., Persons).
        /// </summary>
        [Required]
        [JsonPropertyName("label")]
        public required string Label { get; set; }

        /// <summary>
        /// Optional symbol for the unit (e.g., %, €).
        /// </summary>
        [JsonPropertyName("symbol")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Symbol { get; set; }

        /// <summary>
        /// Optional symbol position relative to value. Allowed values: start, end.
        /// </summary>
        [JsonPropertyName("position")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Position { get; set; }

        /// <summary>
        /// Number of decimals to display for this unit (>= 0).
        /// </summary>
        [Required]
        [JsonPropertyName("decimals")]
        public required int Decimals { get; set; }
    }
}
