using System.ComponentModel.DataAnnotations;

namespace PxApi.Models.Search
{
    /// <summary>
    /// Response envelope for metadata search results, including query echo and paging information.
    /// </summary>
    public class SearchResponse
    {
        /// <summary>
        /// Echo of the effective query parameters that were used.
        /// </summary>
        [Required]
        public required SearchQueryInfo Query { get; set; }

        /// <summary>
        /// The search result items matching the query.
        /// </summary>
        [Required]
        public required List<SearchResultItem> Results { get; set; }

        /// <summary>
        /// Paging information for the result set.
        /// </summary>
        [Required]
        public required PagingInfo PagingInfo { get; set; }
    }
}
