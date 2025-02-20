using Px.Utils.Language;

namespace PxApi.DataSources
{
    public class GroupingCacheItem
    {
        public string Code { get; set; }
        public MultilanguageString Name { get; set; }
        public string GroupingCode { get; set; }
        public MultilanguageString GroupingName { get; set; }
    }
}
