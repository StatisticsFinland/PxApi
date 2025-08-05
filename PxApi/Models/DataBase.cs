using Px.Utils.Language;

namespace PxApi.Models
{
    /// <summary>
    /// Stores metadata information about a database.
    /// </summary>
    public class DataBaseMeta
    {
        /// <summary>
        /// Unique identifier for the database.
        /// </summary>
        public required string Id { get; init; }

        /// <summary>
        /// Multilanguage name of the database as a <see cref="MultilanguageString"/>.
        /// </summary>
        public required MultilanguageString Name { get; init; }

        /// <summary>
        /// Multilanguage description of the database as a <see cref="MultilanguageString"/>.
        /// </summary>
        public required MultilanguageString Description { get; init; }
    }
}
