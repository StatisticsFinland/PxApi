using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PxApi.Models
{
    /// <summary>
    /// A value of a dimension.
    /// </summary>
    public class Value
    {
        /// <summary>
        /// Unique identifier among the values of the dimension.
        /// </summary>
        [Required]
        public required string Code { get; set; }

        /// <summary>
        /// Name of the value.
        /// </summary>
        [Required]
        public required string Name { get; set; }

        /// <summary>
        /// Additional information regarding the value.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public required string? Note { get; set; }
    }
}
