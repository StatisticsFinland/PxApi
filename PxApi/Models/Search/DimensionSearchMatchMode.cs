using System.Text.Json.Serialization;

namespace PxApi.Models.Search
{
    /// <summary>
    /// Defines the match mode for dimension searches.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DimensionSearchMatchMode
    {
        /// <summary>
        /// Match only dimension labels and metadata.
        /// </summary>
        Name,

        /// <summary>
        /// Match only dimension values.
        /// </summary>
        Value,

        /// <summary>
        /// Match only dimension descriptions.
        /// </summary>
        Description,

        /// <summary>
        /// Match both dimension labels and values.
        /// </summary>
        Any
    }
}
