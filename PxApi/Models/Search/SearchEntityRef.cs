using System.ComponentModel.DataAnnotations;

namespace PxApi.Models.Search
{
    /// <summary>
    /// Reusable reference to a metadata entity such as a database, table, or value.
    /// </summary>
    public class SearchEntityRef
    {
        /// <summary>
        /// Unique identifier of the entity.
        /// </summary>
        [Required]
        public required string Id { get; set; }

        /// <summary>
        /// Localized display name of the entity.
        /// </summary>
        [Required]
        public required string Name { get; set; }

        /// <summary>
        /// Optional localized note or annotation associated with the entity.
        /// </summary>
        public string? Note { get; set; }
    }
}
