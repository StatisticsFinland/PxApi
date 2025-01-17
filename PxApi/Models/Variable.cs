using Px.Utils.Models.Metadata.Enums;
using System.Text.Json.Serialization;

namespace PxApi.Models
{
    public class Variable : VariableBase
    {
        public required DimensionType Type { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public required List<Value>? Values { get; set; }
    }
}
