using System.ComponentModel.DataAnnotations;

namespace PxApi.Models
{
    /// <summary>
    /// Represents summary information about a classification: its name and the number of values it contains.
    /// </summary>
    public class ClassificationInfo
    {
        /// <summary>
        /// Localized display name of the classification.
        /// </summary>
        [Required]
        public required string Name { get; set; }

        /// <summary>
        /// Number of values in the classification.
        /// </summary>
        [Required]
        public required int Size { get; set; }
    }
}
