using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PxApi.Models
{
    /// <summary>
    /// Represents a table and its essential metadata used in listings.
    /// </summary>
    public class TableListingItem
    {
        /// <summary>
        /// The unique identifier for the table.
        /// </summary>
        [Required]
        public required string ID { get; set; }

        /// <summary>
        /// Human-readable short name of the table.
        /// </summary>
        [Required]
        public required string Name { get; set; }

        /// <summary>
        /// Current lifecycle status of the table. If <see cref="TableStatus.Error"/>, <see cref="Title"/> and <see cref="LastUpdated"/> are null.
        /// </summary>
        [Required]
        public required TableStatus Status { get; set; }

        /// <summary>
        /// Localized title of the table, if available.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Title { get; set; }

        /// <summary>
        /// Last update timestamp (UTC) or null if not available.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? LastUpdated { get; set; }

        /// <summary>
        /// HATEOAS links related to the table (e.g., self, metadata, data endpoints).
        /// </summary>
        [Required]
        public required List<Link> Links { get; set; }
    }
}
