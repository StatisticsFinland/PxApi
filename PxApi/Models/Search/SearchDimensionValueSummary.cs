using System.ComponentModel.DataAnnotations;

namespace PxApi.Models.Search
{
    /// <summary>
    /// Represents a dimension value summary in search results.
    /// </summary>
    public class SearchDimensionValueSummary
    {
        /// <summary>
        /// Value identifier.
        /// </summary>
        [Required]
        public required string ID { get; set; }

        /// <summary>
        /// Value label.
        /// </summary>
        [Required]
        public required string Name { get; set; }
    }
}
