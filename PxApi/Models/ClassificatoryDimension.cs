using Px.Utils.Models.Metadata.Enums;
using System.Text.Json.Serialization;

namespace PxApi.Models
{
    /// <summary>
    /// A classificatory dimension, describes one dimension of the table.
    /// </summary>
    public class ClassificatoryDimension : DimensionBase
    {
        /// <summary>
        /// More accurate type of the dimension, e.g. ordinal, nominal, etc.
        /// </summary>
        public required DimensionType Type { get; set; }

        /// <summary>
        /// Values of this dimension.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public required List<Value>? Values { get; set; }
    }
}
