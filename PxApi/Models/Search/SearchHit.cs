using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PxApi.Models.Search
{
    /// <summary>
    /// Represents a lightweight search hit before metadata enrichment.
    /// </summary>
    public class SearchHit
    {
        /// <summary>
        /// Optional numeric relevance score used for ordering results.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Score { get; set; }

        /// <summary>
        /// Reference to the database this hit belongs to.
        /// </summary>
        [Required]
        public required SearchDatabaseRef Database { get; set; }

        /// <summary>
        /// PX file identifier of the matched table.
        /// </summary>
        [Required]
        public required string TableId { get; set; }

        /// <summary>
        /// Describes where and how the search hit occurred.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<MatchInfo>? Matches { get; set; }
    }
}
