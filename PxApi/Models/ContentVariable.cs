using System.Text.Json.Serialization;

namespace PxApi.Models
{
    public class ContentVariable : VariableBase
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public required List<ContentValue>? Values { get; set; }
    }
}
