namespace PxApi.Models
{
    public class PagingInfo
    {
        public required int CurrentPage { get; set; }
        public required int PageSize { get; set; }
        public required int TotalItems { get; set; }
        public required int MaxPageSize { get; set; }
    }
}
