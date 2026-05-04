using System.ComponentModel.DataAnnotations;

namespace PxApi.Models
{
    /// <summary>
    /// Contains essential summary information about a statistical table, including
    /// its identification, measures, time range, classifications, and update timestamp.
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
        /// Measure names and their units of measurement.
        /// </summary>
        [Required]
        public required List<MeasureInfo> Measures { get; set; }

        /// <summary>
        /// Time range covered by the table's time dimension.
        /// Empty values indicate that the table does not contain a time dimension.
        /// </summary>
        [Required]
        public required TimeRange TimeRange { get; set; }

        /// <summary>
        /// Names and sizes of the classification dimensions (excluding measure and time).
        /// </summary>
        [Required]
        public required List<ClassificationInfo> Classifications { get; set; }

        /// <summary>
        /// Last update timestamp (UTC) derived from the measure values.
        /// </summary>
        [Required]
        public required DateTime LastUpdated { get; set; }
    }
}
