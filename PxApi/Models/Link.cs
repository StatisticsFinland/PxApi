namespace PxApi.Models
{
    /// <summary>
    /// A link to a resource
    /// For use in the HATEOAS implementation.
    /// </summary>
    public class Link
    {
        /// <summary>
        /// The href to the resource
        /// </summary>
        public required string Href { get; set; }

        /// <summary>
        /// The relation between the object containing the link and the resource
        /// </summary>
        public required string Rel { get; set;  }

        /// <summary>
        /// The method to use when accessing the resource, e.g. GET, POST.
        /// </summary>
        public required string Method { get; set;  }
    }
}
