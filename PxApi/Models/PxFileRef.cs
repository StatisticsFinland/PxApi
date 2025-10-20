using PxApi.Configuration;

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
        /// Original file path of the Px file.
        /// </summary>
        public string FilePath { get; init; }
        
        /// <summary>
        /// <see cref="DataBaseRef"/> reference to the database that the Px file belongs to.
        /// </summary>
        public DataBaseRef DataBase { get; init; }

        private readonly static char[] _allowedIdChars = ['_', '-'];

        private PxFileRef(string id, string filePath, DataBaseRef dataBase)
        {
            Id = id;
            FilePath = filePath;
            DataBase = dataBase;
        }

        /// <summary>
        /// Creates a new instance of <see cref="PxFileRef"/> with the specified file path, using database configuration to parse the ID.
        /// </summary>
        /// <param name="fullFilePath">Full path to the Px file.</param>
        /// <param name="database"><see cref="DataBaseRef"/> reference to the database that the Px file belongs to.</param>
        /// <returns>A new instance of <see cref="PxFileRef"/>.</returns>
        /// <exception cref="ArgumentException">If the parsed id is null, whitespace, contains invalid characters or exceeds 50 characters.</exception>
        public static PxFileRef CreateFromPath(string fullFilePath, DataBaseRef database)
        {
            string fileName = Path.GetFileNameWithoutExtension(fullFilePath);
            if(string.IsNullOrWhiteSpace(fileName) || fileName.Length > 50)
            {
                throw new ArgumentException("PxFile id cannot be null, whitespace or exceed 50 characters.");
            }
            if (!fileName.All(s => char.IsLetterOrDigit(s) || _allowedIdChars.Contains(s)))
            {
                throw new ArgumentException("PxFile id must contain only letters or numbers.");
            }
            return new PxFileRef(fileName, fullFilePath, database);
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
