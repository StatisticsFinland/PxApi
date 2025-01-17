namespace PxApi.Models.V1
{
    public class ContentDimensionValueV1 : DimensionValueV1
    {
        public required string Unit { get; set; }

        public required int Precision { get; set; }

        public required string Source { get; set; }

        public required DateTime LastUpdated { get; set; }
    }
}
