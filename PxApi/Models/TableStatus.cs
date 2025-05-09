using System.Text.Json.Serialization;

namespace PxApi.Models
{
    /// <summary>
    /// State of a table.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TableStatus
    {
        /// <summary>
        /// This table is archived.
        /// </summary>
        Archived,

        /// <summary>
        /// This table is the latest updated version.
        /// </summary>
        Current,

        /// <summary>
        /// This table is discontinued but not archived.
        /// </summary>
        Discontinued,

        /// <summary>
        /// This table is currently not available.
        /// </summary>
        Error,

        /// <summary>
        /// This table is moved to another location.
        /// </summary>
        Moved
    }
}
