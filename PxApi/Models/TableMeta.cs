using System.ComponentModel.DataAnnotations;

namespace PxApi.Models
{
    /// <summary>
    /// Contains metadata about a specific table.
    /// </summary>
    public class TableMeta
    {
        /// <summary>
        /// Unique identifier for the table.
        /// </summary>
        [Required]
        public required string ID { get; set; }

        /// <summary>
        /// Short summary of the tables contents.
        /// </summary>
        public string? Contents { get; set; }

        /// <summary>
        /// Descriptive title for the table.
        /// </summary>
        [Required]
        public required string Title { get; set; }

        /// <summary>
        /// A optional note about the table.
        /// </summary>
        public string? Note { get; set; }

        /// <summary>
        /// When was the table last modified.
        /// </summary>
        [Required]
        public required DateTime LastModified { get; set; }

        /// <summary>
        /// the first (oldes) period in the table.
        /// </summary>
        [Required]
        public required string FirstPeriod { get; set; }

        /// <summary>
        /// The last (newest) period in the table.
        /// </summary>
        [Required]
        public required string LastPeriod { get; set; }

        /// <summary>
        /// Content dimension of the table. Contains additional metadata compared to the other dimensions.
        /// </summary>
        [Required]
        public required ContentDimension ContentDimension { get; set; }

        /// <summary>
        /// Time dimension of the table. Defines the time series of the table.
        /// </summary>
        [Required]
        public required TimeDimension TimeDimension { get; set; }

        /// <summary>
        /// Other dimensions. Excluding the content and time dimensions.
        /// </summary>
        [Required]
        public required List<Dimension> ClassificatoryDimensions { get; set; }

        /// <summary>
        /// List of groups that the table is part of.
        /// </summary>
        [Required]
        public required List<TableGroup> Groupings { get; set; }

        /// <summary>
        /// Links to resources related to this table.
        /// </summary>
        [Required]
        public required List<Link> Links { get; set; }
    }
}
