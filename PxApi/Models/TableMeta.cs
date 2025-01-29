namespace PxApi.Models
{
    /// <summary>
    /// Contains metadata about a specific table.
    /// </summary>
    public class TableMeta
    {
        /// <summary>
        /// Unique identifier for the table.
        /// </summary>
        public required string? ID { get; set; }

        /// <summary>
        /// Short summary of the tables contents.
        /// </summary>
        public required string? Contents { get; set; }

        /// <summary>
        /// A longer description of the table.
        /// </summary>
        public required string? Description { get; set; }

        /// <summary>
        /// A optional note about the table.
        /// </summary>
        public required string? Note { get; set; }

        /// <summary>
        /// When was the table last modified.
        /// </summary>
        public required DateTime LastModified { get; set; }

        /// <summary>
        /// the first (oldes) period in the table.
        /// </summary>
        public required string FirstPeriod { get; set; }

        /// <summary>
        /// The last (newest) period in the table.
        /// </summary>
        public required string LastPeriod { get; set; }

        /// <summary>
        /// Content variable of the table. Contains additional metadata compared to the other variables.
        /// </summary>
        public required ContentVariable ContentVariable { get; set; }

        /// <summary>
        /// Time variable of the table. Defines the time series of the table.
        /// </summary>
        public required TimeVariable TimeVariable { get; set; }

        /// <summary>
        /// Other variables. Excluding the content and time variables.
        /// </summary>
        public required List<Variable> ClassificatoryVariables { get; set; }

        /// <summary>
        /// Links to resources related to this table.
        /// </summary>
        public required List<Link> Links { get; set; }
    }
}
