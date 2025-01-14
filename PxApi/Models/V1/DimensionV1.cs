using Px.Utils.Models.Metadata.Enums;
using System.Text.Json.Serialization;

namespace PxApi.Models.V1
{
    public class DimensionV1
    {
        public required string? Note { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public required TimeDimensionInterval? Interval { get; set; }

        public required string Code { get; set; }

        public required string Name { get; set; }

        public required DimensionType Type { get; set; }

        public int Size { get; set; }

        public required List<DimensionValueV1> Values { get; set; }

        public required string Url { get; set; }
    }
}
