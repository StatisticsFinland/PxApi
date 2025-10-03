using System.ComponentModel.DataAnnotations;

namespace PxApi.Models
{
    /// <summary>
    /// Value of a content dimension, contains additional metadata compared to a regular dimension values.
    /// </summary>
    public class ContentValue : Value
    {
        /// <summary>
        /// The unit of the value, e.g. €, %, Index points, etc.
        /// </summary>
        [Required]
        public required string Unit { get; set; }

        /// <summary>
        /// The precision of the datapoints defined by this values.
        /// </summary>
        [Range(0, 15)]
        public required int Precision { get; set; }

        /// <summary>
        /// The source of the data, e.g. Statistics Finland.
        /// </summary>
        [Required]
        public required string Source { get; set; }

        /// <summary>
        /// The date when the data defined by this value was last updated.
        /// </summary>
        public required DateTime LastUpdated { get; set; }
    }
}
