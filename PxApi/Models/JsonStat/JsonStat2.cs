using Px.Utils.Models.Data.DataValue;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PxApi.Models.JsonStat
{
    /// <summary>
    /// Represents a JSON-stat 2.0 dataset response. See https://json-stat.org/ for full specification details.
    /// </summary>
    public class JsonStat2
    {
        /// <summary>
        /// The JSON-stat version string.
        /// </summary>
        [Required]
        [JsonPropertyName("version")]
        public string Version { get; init; } = "2.0";

        /// <summary>
        /// The object class of the JSON-stat response. For data queries this is always 'dataset'.
        /// </summary>
        [Required]
        [JsonPropertyName("class")]
        public string Class { get; } = "dataset";

        /// <summary>
        /// Ordered list of dimension identifiers. The order defines the major-to-minor traversal used to produce the <see cref="Value"/> array.
        /// </summary>
        [Required]
        [JsonPropertyName("id")]
        public required string[] Id { get; init; }

        /// <summary>
        /// Short, language-dependent dataset label.
        /// </summary>
        [Required]
        [JsonPropertyName("label")]
        public required string Label { get; init; }

        /// <summary>
        /// Optional annotations about the dataset (language dependent).
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
        /// Timestamp indicating when the dataset was last updated (ISO 8601, UTC recommended).
        /// </summary>
        [JsonPropertyName("updated")]
        public required string Updated { get; init; }

        /// <summary>
        /// Map from dimension identifier to its metadata (categories, labels, units, etc.).
        /// </summary>
        [Required]
        [JsonPropertyName("dimension")]
        public required Dictionary<string, Dimension> Dimension { get; set; }

        /// <summary>
        /// Flattened array of data values following the Cartesian product of dimensions in the order given by <see cref="Id"/>. Missing values are encoded as null.
        /// </summary>
        [Required]
        [JsonPropertyName("value")]
        public required DoubleDataValue[] Value { get; set; }

        /// <summary>
        /// Optional status information for data values. Keys are zero-based indexes into <see cref="Value"/>; values are arbitrary status codes.
        /// </summary>
        [JsonPropertyName("status")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<int, string>? Status { get; set; }

        /// <summary>
        /// Optional extension object for additional, non-standard metadata. Keys and value types are unrestricted.
        /// </summary>
        [JsonPropertyName("extension")]
        public Dictionary<string, object>? Extension { get; set; }

        /// <summary>
        /// Size of each dimension in the same order as <see cref="Id"/>. Product of all elements equals <see cref="Value"/> length.
        /// </summary>
        [Required]
        [JsonPropertyName("size")]
        public List<int> Size { get; set; } = [];

        /// <summary>
        /// Optional mapping declaring special roles for certain dimensions (e.g. time, metric). Each role maps to a list of dimension identifiers.
        /// </summary>
        [JsonPropertyName("role")]
        public Dictionary<string, List<string>>? Role { get; set; }
    }
}