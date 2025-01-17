using System.Text.Json.Serialization;

namespace PxApi.Models.V1
{
    public class ContentDimensionV1 : DimensionBaseV1
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public required List<ContentDimensionValueV1>? Values { get; set; }
    }
}
