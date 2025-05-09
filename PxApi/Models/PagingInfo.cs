using System.ComponentModel.DataAnnotations;

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
        [Range(1, int.MaxValue)]
        public required int CurrentPage { get; set; }

        /// <summary>
        /// The number of items per page.
        /// </summary>
        [Range(1, 100)]
        public required int PageSize { get; set; }

        /// <summary>
        /// The total number of items that can be paged through.
        /// </summary>
        [Range(0, int.MaxValue)]
        public required int TotalItems { get; set; }
    }
}
