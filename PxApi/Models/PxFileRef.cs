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
        /// Optional grouping identifier extracted from the filename.
        /// </summary>
        public string? GroupingId { get; init; }

        /// <summary>
        /// <see cref="DataBaseRef"/> reference to the database that the Px file belongs to.
        /// </summary>
        public DataBaseRef DataBase { get; init; }

        private PxFileRef(string id, string filePath, string? groupingId, DataBaseRef dataBase)
        {
            Id = id;
            FilePath = filePath;
            GroupingId = groupingId;
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
            return new PxFileRef(id, id, null, database);
        }

        /// <summary>
        /// Creates a new instance of <see cref="PxFileRef"/> with the specified file path, using database configuration to parse the ID.
        /// </summary>
        /// <param name="fullFilePath">Full path to the Px file.</param>
        /// <param name="database"><see cref="DataBaseRef"/> reference to the database that the Px file belongs to.</param>
        /// <param name="config">Database configuration for file name parsing.</param>
        /// <returns>A new instance of <see cref="PxFileRef"/>.</returns>
        /// <exception cref="ArgumentException">If the parsed id is null, whitespace, contains invalid characters or exceeds 50 characters.</exception>
        public static PxFileRef Create(string fullFilePath, DataBaseRef database, DataBaseConfig config)
        {
            string fileName = Path.GetFileNameWithoutExtension(fullFilePath);
            
            // Parse the ID and grouping ID from the filename
            string id = ParseFilename(fileName, config, config.FilenameIdPartIndex);
            string? groupingId = null;
            
            // Only try to get grouping ID if the configuration specifies it
            if (config.FilenameGroupingPartIndex.HasValue)
            {
                groupingId = ParseFilename(fileName, config, config.FilenameGroupingPartIndex);
            }

            if (string.IsNullOrWhiteSpace(id) || id.Length > 50)
                throw new ArgumentException("Parsed Id cannot be null, whitespace or too long.");

            if(id.Any(s => !char.IsLetterOrDigit(s)))
            {
                throw new ArgumentException("PxFile id must containt only letters or numbers.");
            }
            return new PxFileRef(id, fullFilePath, groupingId, database);
        }

        /// <summary>
        /// Parses a filename using the database configuration to extract a part based on the specified index.
        /// </summary>
        /// <param name="fileName">The filename to parse.</param>
        /// <param name="config">The database configuration.</param>
        /// <param name="partIndex">The index of the part to extract or null to use the whole filename.</param>
        /// <returns>The parsed part from the filename.</returns>
        private static string ParseFilename(string fileName, DataBaseConfig config, int? partIndex)
        {
            if (config.FilenameSeparator == null || !partIndex.HasValue)
            {
                return fileName;
            }

            string[] parts = fileName.Split(config.FilenameSeparator.Value);
            
            if (parts.Length == 0)
            {
                return fileName;
            }

            int index = partIndex.Value;
            // If index is -1, use the last part
            if (index == -1)
            {
                index = parts.Length - 1;
            }
            
            if (index >= 0 && index < parts.Length)
            {
                return parts[index];
            }
            
            // If index is out of range, return the original name
            return fileName;
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
