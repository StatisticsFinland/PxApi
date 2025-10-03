using System.ComponentModel.DataAnnotations;

namespace PxApi.Models
{
    /// <summary>
    /// A base class for dimensions.
    /// </summary>
    public abstract class DimensionBase
    {
        /// <summary>
        /// Unique identifier of the dimension.
        /// </summary>
        [Required]
        public required string Code { get; set; }

        /// <summary>
        /// Name of the dimension.
        /// </summary>
        [Required]
        public required string Name { get; set; }

        /// <summary>
        /// Additional information regarding the dimension.
        /// </summary>
        public required string? Note { get; set; }

        /// <summary>
        /// How many values the dimension has.
        /// </summary>
        [Range(1, int.MaxValue)]
        public required int Size { get; set; }

        /// <summary>
        /// Links to resources related to this dimension.
        /// </summary>
        [Required]
        public required List<Link> Links { get; set; }
    }
}
