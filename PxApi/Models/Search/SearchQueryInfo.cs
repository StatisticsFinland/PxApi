using System.ComponentModel.DataAnnotations;

namespace PxApi.Models.Search
{
    /// <summary>
    /// Echo of the effective query parameters used for the search request.
    /// </summary>
    public class SearchQueryInfo
    {
        /// <summary>
        /// The search query string that was used.
        /// </summary>
        [Required]
        public required string Q { get; set; }

        /// <summary>
        /// The result types that were searched for.
        /// </summary>
        [Required]
        public required List<SearchResultType> Types { get; set; }

        /// <summary>
        /// The language code that was used for the search.
        /// </summary>
        [Required]
        public required string Lang { get; set; }
    }
}
