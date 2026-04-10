namespace PxApi.Models.Search
{
    /// <summary>
    /// Describes where and how a search hit occurred within a result.
    /// </summary>
    public class MatchInfo
    {
        /// <summary>
        /// Machine-friendly identifier of the matched field (e.g. "table.name", "dimension.unit.label").
        /// </summary>
        public required string Path { get; set; }

        /// <summary>
        /// Describes how the query matched the target text.
        /// </summary>
        public MatchType MatchType { get; set; }

        /// <summary>
        /// The text that matched the search query.
        /// </summary>
        public string? MatchedText { get; set; }
    }
}
