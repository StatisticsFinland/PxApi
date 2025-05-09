using Px.Utils.Models.Metadata.Enums;
using System.Text.Json.Serialization;

namespace PxApi.Models
{
    /// <summary>
    /// A time variable, defines the time series of the data.
    /// </summary>
    public class TimeVariable : VariableBase
    {
        /// <summary>
        /// Length of the the period defined by one value of the variable.
        /// </summary>
        public required TimeDimensionInterval Interval { get; set; }

        /// <summary>
        /// Values of this variable.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public required List<Value>? Values { get; set; }
    }
}
