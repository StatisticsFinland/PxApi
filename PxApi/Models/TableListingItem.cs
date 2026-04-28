using System.ComponentModel.DataAnnotations;

namespace PxApi.Models
{
    /// <summary>
    /// Represents a table and its essential metadata used in listings.
    /// </summary>
    public class TableListingItem
    {
        /// <summary>
        /// Essential table information including code, name, dimensions, and update timestamp.
        /// </summary>
        [Required]
        public required TableSummary Table { get; set; }

        /// <summary>
        /// HATEOAS links related to the table (e.g., self, metadata, data endpoints).
        /// </summary>
        [Required]
        public required List<Link> Links { get; set; }
    }
}
