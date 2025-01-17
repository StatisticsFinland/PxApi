using Px.Utils.Models.Metadata.Enums;

namespace PxApi.Models.V1
{
    public class TimeDimensionV1 : DimensionBaseV1
    {
        public required TimeDimensionInterval Interval { get; set; }

        public required List<DimensionValueV1>? Values { get; set; }
    }
}
