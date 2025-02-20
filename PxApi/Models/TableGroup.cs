namespace PxApi.Models
{
    public class TableGroup
    {
        public required string Code { get; set; }
        public required string Name { get; set; }
        public required string GroupingCode { get; set; }
        public required string GroupingName { get; set; }
        public required List<Link> Links { get; set; }
    }
}
