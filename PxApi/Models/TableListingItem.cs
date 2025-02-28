using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PxApi.Models
{
    /// <summary>
    /// Reresents a table and its essential metadata meant for listing.
    /// </summary>
    public class TableListingItem
    {
        /// <summary>
        /// The unique identifier for the table.
        /// </summary>
        [Required]
        public required string ID { get; set;  }

        /// <summary>
        /// The name of the table.
        /// </summary>
        [Required]
        public required string Name { get; set; }

        /// <summary>
        /// The status of the table. If the <see cref="TableStatus.Error"/>, <see cref="Title"/> and <see cref="LastUpdated"/> are null.
        /// </summary>
        public required TableStatus Status { get; set; }

        /// <summary>
        /// The title of the table, if available.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Title { get; set; }

        /// <summary>
        /// When was the table last updated, null if the information is not available.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? LastUpdated { get; set; }

        /// <summary>
        /// Links to additional resources related to this table.
        /// </summary>
        [Required]
        public required List<Link> Links { get; set; }
    }
}
