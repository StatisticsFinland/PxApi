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

        private PxFileRef(string id, DataBaseRef dataBase)
        {
            Id = id;
            DataBase = dataBase;
        }

        /// <summary>
        /// Creates a new instance of <see cref="PxFileRef"/> with the specified id and database reference.
        /// </summary>
        /// <param name="id">Unique identifier for the Px file.</param>
        /// <param name="database"><see cref="DataBaseRef"/> reference to the database that the Px file belongs to.</param>
        /// <returns>A new instance of <see cref="PxFileRef"/>.</returns>
        /// <exception cref="ArgumentException">If the id is null, whitespace, contains invalid characters or exceeds 50 characters.</exception>
        public static PxFileRef Create(string id, DataBaseRef database)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 50)
                throw new ArgumentException("Id cannot be null, whitespace or too long.");

            if(id.Any(s => !char.IsLetterOrDigit(s)))
            {
                throw new ArgumentException("PxFile id must containt only letters or numbers.");
            }
            return new PxFileRef(id, database);
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
