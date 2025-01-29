using Px.Utils.Models.Metadata.Enums;
using System.Text.Json.Serialization;

namespace PxApi.Models
{
    /// <summary>
    /// A classificatory variable, describes one dimension of the table.
    /// </summary>
    public class Variable : VariableBase
    {
        /// <summary>
        /// More accurate type of the variable, e.g. ordinal, nominal, etc.
        /// </summary>
        public required DimensionType Type { get; set; }

        /// <summary>
        /// Values of this variable.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public required List<Value>? Values { get; set; }
    }
}
