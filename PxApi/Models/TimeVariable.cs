using Px.Utils.Models.Metadata.Enums;
using System.Text.Json.Serialization;

namespace PxApi.Models
{
    public class TimeVariable : VariableBase
    {
        public required TimeDimensionInterval Interval { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public required List<Value>? Values { get; set; }
    }
}
