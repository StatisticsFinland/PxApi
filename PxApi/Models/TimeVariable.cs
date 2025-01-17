using Px.Utils.Models.Metadata.Enums;

namespace PxApi.Models
{
    public class TimeVariable : VariableBase
    {
        public required TimeDimensionInterval Interval { get; set; }

        public required List<Value>? Values { get; set; }
    }
}
