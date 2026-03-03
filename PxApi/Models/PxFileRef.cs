namespace PxApi.Models
{
    /// <summary>
    /// Stores information that reference to a Px file.
    /// </summary>
    public readonly record struct PxFileRef
    {
        /// <summary>
        /// Unique identifier for the Px file.
        /// </summary>
        public string Id { get; init; }

        /// <summary>
        /// <see cref="DataBaseRef"/> reference to the database that the Px file belongs to.
        /// </summary>
        public DataBaseRef DataBase { get; init; }

        /// <summary>
        /// The optional hierarchy of the px file. Each level is separated by '/'.
        /// </summary>
        public string? Hierarchy { get; init; }
        
        private readonly static char[] _allowedIdChars = ['_', '-'];

        private PxFileRef(string id, DataBaseRef dataBase, string? hierarchy = null)
        {
            Id = id;
            DataBase = dataBase;
            Hierarchy = hierarchy;
        }

        /// <summary>
        /// Creates a new instance of <see cref="PxFileRef"/> with the specified file path, using database configuration to parse the ID.
        /// </summary>
        /// <param name="tableId">The unique identifier for the Px file. Must not be null, whitespace, or exceed 50 characters, and may only contain letters, digits, '_' or '-'.</param>
        /// <param name="database"><see cref="DataBaseRef"/> reference to the database that the Px file belongs to.</param>
        /// <param name="hierarchy">Optional array of hierarchy path segments. Each segment may only contain letters, digits, '_' or '-'. The array length must not exceed 100.</param>
        /// <returns>A new instance of <see cref="PxFileRef"/>.</returns>
        /// <exception cref="ArgumentException">If the parsed id is null, whitespace, contains invalid characters or exceeds 50 characters.</exception>
        public static PxFileRef ValidateAndCreate(string tableId, DataBaseRef database, string[]? hierarchy = null)
        {
            if(string.IsNullOrWhiteSpace(tableId) || tableId.Length > 50)
            {
                throw new ArgumentException("PxFile id cannot be null, whitespace or exceed 50 characters.");
            }

            if (hierarchy is not null && hierarchy.Length > 100)
            {
                throw new ArgumentException("PxFile hierarchy length cannot exceed 100 characters.");
            }

            if (!tableId.All(s => char.IsLetterOrDigit(s) || _allowedIdChars.Contains(s)))
            {
                throw new ArgumentException("PxFile id must contain only letters, digits, '_' or '-'.");
            }

            if(hierarchy is not null && !hierarchy.All(s => s.All(c => char.IsLetterOrDigit(c) || _allowedIdChars.Contains(c))))
            {
                throw new ArgumentException("PxFile hierarchy must contain only letters, digits, '_', '-' or directory separator characters.");
            }

            return new PxFileRef(tableId, database, hierarchy is null ? null : string.Join('/', hierarchy));
        }

        /// <summary>
        /// Returns the hierarchy split into its individual levels.
        /// </summary>
        /// <returns>An array of hierarchy level strings, or <c>null</c> if <see cref="Hierarchy"/> is <c>null</c>.</returns>
        public readonly string[]? GetHierarchyLevels()
        {
            return Hierarchy?.Split('/');
        }

        /// <summary>
        /// Gets a hash code for the current instance based on the Id and DataBase.
        /// </summary>
        /// <returns>Hash code for the current instance.</returns>
        public readonly override int GetHashCode()
        {
            return HashCode.Combine(Id, DataBase);
        }
    }
}
