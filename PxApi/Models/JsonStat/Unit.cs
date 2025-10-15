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
        /// The label of the unit.
        /// </summary>
        [Required]
        [JsonPropertyName("label")]
        public required string Label { get; set; }

        /// <summary>
        /// The symbol for the unit.
        /// </summary>
        [JsonPropertyName("symbol")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Symbol { get; set; }

        /// <summary>
        /// The position of the symbol (start, end).
        /// </summary>
        [JsonPropertyName("position")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Position { get; set; }

        /// <summary>
        /// The number of decimals to display for this unit.
        /// </summary>
        [Required]
        [JsonPropertyName("decimals")]
        public required int Decimals { get; set; }
    }
}
