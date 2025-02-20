namespace PxApi.Models
{
    /// <summary>
    /// Represents a list of tables and their essential metadata with paging information.
    /// </summary>
    public class PagedTableList
    {
        /// <summary>
        /// List of tables and their essential metadata.
        /// </summary>
        public required List<TableListingItem> Tables { get; set; }

        /// <summary>
        /// Paging information for the list of tables.
        /// </summary>
        public required PagingInfo PagingInfo { get; set; }
    }
}
