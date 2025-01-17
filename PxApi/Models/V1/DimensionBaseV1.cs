using Px.Utils.Models.Metadata.Enums;

namespace PxApi.Models.V1
{
    public class DimensionBaseV1
    {
        public required string Code { get; set; }

        public required string Name { get; set; }

        public required string? Note { get; set; }

        public int Size { get; set; }

        public required string Url { get; set; }
    }
}
