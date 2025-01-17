using System.Text.Json.Serialization;

namespace PxApi.Models
{
    public class Value
    {
        public required string Code { get; set; }

        public required string Name { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public required string? Note { get; set; }
    }
}
