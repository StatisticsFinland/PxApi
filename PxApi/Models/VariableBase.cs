namespace PxApi.Models
{
    public class VariableBase
    {
        public required string Code { get; set; }

        public required string Name { get; set; }

        public required string? Note { get; set; }

        public required int Size { get; set; }

        public required List<Link> Links { get; set; }
    }
}
