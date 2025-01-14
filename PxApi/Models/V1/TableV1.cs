namespace PxApi.Models.V1
{
    public class TableV1
    {
        public required string? ID { get; set; }

        public required string? Contents { get; set; }

        public required string? Description { get; set; }

        public required string? Note { get; set; }

        public required DateTime LastModified { get; set; }

        public required string FirstPeriod { get; set; }

        public required string LastPeriod { get; set; }

        public required List<DimensionV1> Dimensions { get; set; }
    }
}
