namespace PxApi.Models
{
    /// <summary>
    /// Represents paging information for a list of items.
    /// </summary>
    public class PagingInfo
    {
        /// <summary>
        /// The current page number, starting from 1.
        /// </summary>
        public required int CurrentPage { get; set; }

        /// <summary>
        /// The number of items per page.
        /// </summary>
        public required int PageSize { get; set; }

        /// <summary>
        /// The total number of items that can be paged through.
        /// </summary>
        public required int TotalItems { get; set; }

        /// <summary>
        /// The maximum number of items that can be in a single page.
        /// </summary>
        public required int MaxPageSize { get; set; }
    }
}
