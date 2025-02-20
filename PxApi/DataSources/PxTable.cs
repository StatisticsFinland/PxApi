namespace PxApi.DataSources
{
    /// <summary>
    /// Represents a Px table in one database.
    /// </summary>
    /// <param name="tableId">Unique identifier of the table.</param>
    /// <param name="hierarchy">Additional hierarchical levels related to this table in the database.</param>
    /// <param name="databaseId">Unique identifier of the database.</param>
    public class PxTable(string tableId, List<string> hierarchy, string databaseId)
    {
        /// <summary>
        /// Unique identifier of the table.
        /// </summary>
        public string TableId { get; } = tableId;

        /// <summary>
        /// Additional hierarchical levels related to this table in the database.
        /// </summary>
        public List<string> Hierarchy { get; } = hierarchy;

        /// <summary>
        /// Unique identifier of the database.
        /// </summary>
        public string DatabaseId { get; } = databaseId;
    }
}
