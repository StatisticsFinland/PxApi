using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PxApi.Models
{
    /// <summary>
    /// Contains essential summary information about a statistical table, including
    /// its identification, metrics, time range, dimensions, and update timestamp.
    /// </summary>
    public class TableSummary
    {
        /// <summary>
        /// Unique identifier for the table (e.g., TABLEID from PX metadata).
        /// </summary>
        [Required]
        public required string TableId { get; set; }

        /// <summary>
        /// Localized display title of the table.
        /// </summary>
        [Required]
        public required string Title { get; set; }

        /// <summary>
        /// Metric names and their units of measurement.
        /// </summary>
        [Required]
        public required List<MetricInfo> Metrics { get; set; }

        /// <summary>
        /// Time range covered by the table's time dimension.
        /// Empty values indicate that the table does not contain a time dimension.
        /// </summary>
        [Required]
        public required TimeRange TimeRange { get; set; }

        /// <summary>
        /// Names and sizes of all dimensions, excluding the metric, time, and geographical dimensions.
        /// </summary>
        [Required]
        public required List<DimensionInfo> Dimensions { get; set; }

        /// <summary>
        /// Last update timestamp (UTC) derived from the metric values.
        /// </summary>
        [Required]
        public required DateTime LastUpdated { get; set; }

        /// <summary>
        /// Name of the geographical dimension if it exists, otherwise null.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Geo { get; set; } = null;
    }
}
