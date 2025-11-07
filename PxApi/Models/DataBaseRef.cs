namespace PxApi.Models
{
    /// <summary>
    /// Stores information that references to a database.
    /// </summary>
    public readonly record struct DataBaseRef
    {
        /// <summary>
        /// Gets the unique identifier for the entity.
        /// </summary>
        public string Id { get; init; }

        private DataBaseRef(string id)
        {
            Id = id;
        }

        /// <summary>
        /// Creates a new instance of <see cref="DataBaseRef"/> with the specified id.
        /// </summary>
        /// <param name="id">Unique identifier for the database.</param>
        /// <returns>A new instance of <see cref="DataBaseRef"/>.</returns>
        /// <exception cref="ArgumentException">If the id is null, whitespace, contains invalid characters or exceeds 50 characters.</exception>
        public static DataBaseRef Create(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 50)
                throw new ArgumentException("Id cannot be null, whitespace or too long.");
            if (id.Any(c => !char.IsLetterOrDigit(c)))
            {
                throw new ArgumentException("Database id must contain only letters or numbers.");
            }
            return new DataBaseRef(id);
        }

        /// <summary>
        /// Gets a hash code for the current instance based on the Id.
        /// </summary>
        /// <returns>Hash code for the current instance.</returns>
        public override readonly int GetHashCode() => Id.GetHashCode();
    }
}
