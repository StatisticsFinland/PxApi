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
        public required string ID { get; set; }

        /// <summary>
        /// The name of the table.
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// The title of the table.
        /// </summary>
        public required string Title { get; set; }

        /// <summary>
        /// When was the table last updated.
        /// </summary>
        public required DateTime LastUpdated { get; set; }

        /// <summary>
        /// Links to additional resources related to this table.
        /// </summary>
        public required List<Link> Links { get; set; }
    }
}
