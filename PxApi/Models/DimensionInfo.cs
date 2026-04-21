using System.ComponentModel.DataAnnotations;

namespace PxApi.Models
{
    /// <summary>
    /// Represents summary information about a dimension: its name and the number of values it contains.
    /// </summary>
    public class DimensionInfo
    {
        /// <summary>
        /// Localized display name of the dimension.
        /// </summary>
        [Required]
        public required string Name { get; set; }

        /// <summary>
        /// Number of values in the dimension.
        /// </summary>
        [Required]
        public required int Size { get; set; }
    }
}
