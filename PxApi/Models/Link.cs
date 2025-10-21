using System.ComponentModel.DataAnnotations;

namespace PxApi.Models
{
    /// <summary>
    /// Represents a link to a related resource (HATEOAS).
    /// </summary>
    public class Link
    {
        /// <summary>
        /// Absolute or relative URI of the target resource.
        /// </summary>
        [Required]
        public required string Href { get; set; }

        /// <summary>
        /// Relation type describing how the target resource relates to the current resource (e.g., self, data, metadata).
        /// </summary>
        [Required]
        public required string Rel { get; set; }

        /// <summary>
        /// HTTP method to use when following the link (e.g., GET, POST).
        /// </summary>
        [Required]
        public required string Method { get; set; }
    }
}
