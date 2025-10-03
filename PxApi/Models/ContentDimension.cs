using System.Text.Json.Serialization;

namespace PxApi.Models
{
    /// <summary>
    /// A content dimension, values of this dimension contain additional metadata compared to a regular dimension values.
    /// </summary>
    public class ContentDimension : DimensionBase
    {
        /// <summary>
        /// Values of this content dimension.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public required List<ContentValue>? Values { get; set; }
    }
}
