namespace PxApi.Models
{
    public class PagedTableList
    {
        public required List<TableListingItem> Tables { get; set; }

        public required PagingInfo PagingInfo { get; set; }
    }
}
