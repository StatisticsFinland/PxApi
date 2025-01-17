namespace PxApi.Models
{
    public class TableMeta
    {
        public required string? ID { get; set; }

        public required string? Contents { get; set; }

        public required string? Description { get; set; }

        public required string? Note { get; set; }

        public required DateTime LastModified { get; set; }

        public required string FirstPeriod { get; set; }

        public required string LastPeriod { get; set; }

        public required ContentVariable ContentVariable { get; set; }

        public required TimeVariable TimeVariable { get; set; }

        public required List<Variable> ClassificatoryVariables { get; set; }
    }
}
