using Px.Utils.Language;

namespace PxApi.DataSources
{
    /// <summary>
    /// Serialization model for the grouping information in local filesystem data sources.
    /// </summary>
    public class Groupings
    {
        /// <summary>
        /// The code of the grouping.
        /// </summary>
        public required string Code { get; set; }

        /// <summary>
        /// The name of the grouping.
        /// </summary>
        public required MultilanguageString Name { get; set; }
    }
}
