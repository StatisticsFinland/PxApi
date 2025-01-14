using System.Text.Json.Serialization;

namespace PxApi.Models.V1
{
    public class DimensionValueV1
    {
        public required string Code { get; set; }

        public required string Name { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public required string? Note { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public required string? Unit { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public required int? Precision { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public required string? Source { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public required DateTime? LastUpdated { get; set; }
    }
}
