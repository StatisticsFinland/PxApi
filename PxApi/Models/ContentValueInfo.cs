using System.ComponentModel.DataAnnotations;

namespace PxApi.Models
{
    /// <summary>
    /// Represents a content dimension value with its name and unit of measurement.
    /// </summary>
    public class ContentValueInfo
    {
        /// <summary>
        /// Localized name of the content value (e.g., "Population", "GDP").
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
