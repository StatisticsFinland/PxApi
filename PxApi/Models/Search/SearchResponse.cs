using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using PxApi.Models;

namespace PxApi.Models.Search
{
    /// <summary>
    /// Represents a generic search response payload.
    /// </summary>
    /// <typeparam name="TItem">Result item type.</typeparam>
    public class SearchResponse<TItem>
    {
        /// <summary>
        /// Search result items.
        /// </summary>
        [Required]
        public required List<TItem> Results { get; set; }

        /// <summary>
        /// Paging metadata for the response.
        /// </summary>
        [Required]
        public required PagingInfo PagingInfo { get; set; }
    }
}
