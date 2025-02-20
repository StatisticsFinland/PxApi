namespace PxApi.Models
{
    public class TableListingItem
    {
        public required string ID { get; set; }
        public required string Name { get; set; }
        public required string Title { get; set; }
        public required DateTime LastUpdated { get; set; }
        public required List<Link> Links { get; set; }
    }
}
