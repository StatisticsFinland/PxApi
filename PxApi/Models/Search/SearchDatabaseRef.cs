using System.ComponentModel.DataAnnotations;

namespace PxApi.Models.Search
{
    /// <summary>
    /// Reference to a database returned with a search result.
    /// </summary>
    public class SearchDatabaseRef
    {
        /// <summary>
        /// Unique identifier of the database.
        /// </summary>
        [Required]
        public required string Id { get; set; }

        /// <summary>
        /// Display name of the database.
        /// </summary>
        [Required]
        public required string Name { get; set; }
    }
}
