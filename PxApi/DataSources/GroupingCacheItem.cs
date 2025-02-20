using Px.Utils.Language;

namespace PxApi.DataSources
{
    /// <summary>
    /// For storing grouping information in all available languages.
    /// </summary>
    public class GroupingCacheItem
    {
        /// <summary>
        /// The unique code of this group.
        /// </summary>
        public required string Code { get; set; }

        /// <summary>
        /// The name of this group in all available languages.
        /// </summary>
        public required MultilanguageString Name { get; set; }

        /// <summary>
        /// The unique code of thr grouping this group belongs to.
        /// </summary>
        public required string GroupingCode { get; set; }

        /// <summary>
        /// The name of the grouping this group belongs to in all available languages.
        /// </summary>
        public required MultilanguageString GroupingName { get; set; }
    }
}
