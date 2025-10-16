using Px.Utils.Models.Data.DataValue;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PxApi.Models.JsonStat
{
    /// <summary>
    /// Represents a JSON-stat 2.0 format response.
    /// https://json-stat.org/
    /// </summary>
    public class JsonStat2
    {
        /// <summary>
        /// The version of the JSON-stat implementation.
        /// </summary>
        [Required]
        [JsonPropertyName("version")]
        public string Version { get; init; } = "2.0";

        /// <summary>
        /// The object type of the JSON-stat response (dataset, collection, or dimension).
        /// </summary>
        [Required]
        [JsonPropertyName("class")]
        public string Class { get; } = "dataset";

        /// <summary>
        /// Contains an ordered list of the dimension IDs.
        /// </summary>
        [Required]
        [JsonPropertyName("id")]
        public required string[] Id { get; init; }

        /// <summary>
        /// A very short (one line) descriptive text. Language-dependent.
        /// </summary>
        [Required]
        [JsonPropertyName("label")]
        public required string Label { get; init; }

        /// <summary>
        /// Annotations about the dataset. Language-dependent.
        /// </summary>
        [JsonPropertyName("note")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string[]? Note { get; init; }

        /// <summary>
        /// The source of the dataset. Language-dependent.
        /// </summary>
        [JsonPropertyName("source")]
        public required string Source { get; init; }

        /// <summary>
        /// The time period information for the dataset.
        /// </summary>
        [JsonPropertyName("updated")]
        public required string Updated { get; init; }

        /// <summary>
        /// The dimensions of the dataset.
        /// </summary>
        [Required]
        [JsonPropertyName("dimension")]
        public required Dictionary<string, Dimension> Dimension { get; set; }

        /// <summary>
        /// The actual data values.
        /// </summary>
        [Required]
        [JsonPropertyName("value")]
        public required DoubleDataValue[] Value { get; set; }

        /// <summary>
        /// The status information for the data values.
        /// </summary>
        [JsonPropertyName("status")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<int, string>? Status { get; set; }

        /// <summary>
        /// Additional extension information.
        /// </summary>
        [JsonPropertyName("extension")]
        public Dictionary<string, object>? Extension { get; set; }

        /// <summary>
        /// The size array that represents the number of elements in each dimension.
        /// </summary>
        [Required]
        [JsonPropertyName("size")]
        public List<int> Size { get; set; } = [];

        /// <summary>
        /// The role object for expressing special dimensions.
        /// </summary>
        [JsonPropertyName("role")]
        public Dictionary<string, List<string>>? Role { get; set; }
    }
}