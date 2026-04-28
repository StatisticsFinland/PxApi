using System.ComponentModel.DataAnnotations;

namespace PxApi.Models
{
    /// <summary>
    /// Represents the time range of a table's time dimension.
    /// </summary>
    public class TimeRange
    {
        /// <summary>
        /// Name of the first time period value (e.g., "2000").
        /// </summary>
        [Required]
        public required string From { get; set; }

        /// <summary>
        /// Name of the last time period value (e.g., "2024").
        /// </summary>
        [Required]
        public required string To { get; set; }
    }
}
