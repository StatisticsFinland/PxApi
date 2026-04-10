using System.ComponentModel.DataAnnotations;

namespace PxApi.Models.Search
{
    /// <summary>
    /// Reference to a dimension, optionally including unit information.
    /// </summary>
    public class DimensionRef
    {
        /// <summary>
        /// Unique identifier of the dimension.
        /// </summary>
        [Required]
        public required string Id { get; set; }

        /// <summary>
        /// Localized display name of the dimension.
        /// </summary>
        [Required]
        public required string Name { get; set; }

        /// <summary>
        /// Optional localized note or annotation associated with the dimension.
        /// </summary>
        public string? Note { get; set; }

        /// <summary>
        /// Optional unit information related to the dimension.
        /// </summary>
        public string? Unit { get; set; }
    }
}
