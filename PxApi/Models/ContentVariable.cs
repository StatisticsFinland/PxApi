using System.Text.Json.Serialization;

namespace PxApi.Models
{
    /// <summary>
    /// A content variable, values of this variable contain additional metadata compared to a regular variable values.
    /// </summary>
    public class ContentVariable : VariableBase
    {
        /// <summary>
        /// Values of this content variable.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public required List<ContentValue>? Values { get; set; }
    }
}
