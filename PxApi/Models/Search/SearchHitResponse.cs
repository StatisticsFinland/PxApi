using System.ComponentModel.DataAnnotations;

namespace PxApi.Models.Search
{
    /// <summary>
    /// Response envelope for raw search hits before metadata enrichment.
    /// </summary>
    public class SearchHitResponse
    {
        /// <summary>
        /// Echo of the effective query parameters that were used.
        /// </summary>
        [Required]
        public required SearchQueryInfo Query { get; set; }

        /// <summary>
        /// The raw search hits matching the query.
        /// </summary>
        [Required]
        public required List<SearchHit> Results { get; set; }

        /// <summary>
        /// Paging information for the result set.
        /// </summary>
        [Required]
        public required PagingInfo PagingInfo { get; set; }
    }
}
