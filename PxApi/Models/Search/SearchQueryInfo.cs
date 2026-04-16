using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

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
        /// The search target that was used.
        /// </summary>
        [Required]
        public required SearchTarget Target { get; set; }

        /// <summary>
        /// The language code that was used for the search.
        /// </summary>
        [Required]
        public required string Lang { get; set; }
    }
}
