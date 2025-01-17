using Px.Utils.Models.Metadata.Enums;
using System.Text.Json.Serialization;

namespace PxApi.Models.V1
{
    public class DimensionV1 : DimensionBaseV1
    {
        public required DimensionType Type { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public required List<DimensionValueV1>? Values { get; set; }
    }
}
