using Px.Utils.Models.Metadata.Enums;
using System.Text.Json.Serialization;

namespace PxApi.Models
{
    /// <summary>
    /// A time dimension, defines the time series of the data.
    /// </summary>
    public class TimeDimension : DimensionBase
    {
        /// <summary>
        /// Length of the the period defined by one value of the dimension.
        /// </summary>
        public required TimeDimensionInterval Interval { get; set; }

        /// <summary>
        /// Values of this dimension.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public required List<Value>? Values { get; set; }
    }
}
