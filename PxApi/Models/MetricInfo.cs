using System.ComponentModel.DataAnnotations;

namespace PxApi.Models
{
    /// <summary>
    /// Represents a metric with its name and unit of measurement.
    /// </summary>
    public class MetricInfo
    {
        /// <summary>
        /// Localized name of the metric (e.g., "Population", "GDP").
        /// </summary>
        [Required]
        public required string Name { get; set; }

        /// <summary>
        /// Localized unit of measurement (e.g., "persons", "EUR").
        /// </summary>
        [Required]
        public required string Unit { get; set; }
    }
}
