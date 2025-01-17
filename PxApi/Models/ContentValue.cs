namespace PxApi.Models
{
    public class ContentValue : Value
    {
        public required string Unit { get; set; }

        public required int Precision { get; set; }

        public required string Source { get; set; }

        public required DateTime LastUpdated { get; set; }
    }
}
