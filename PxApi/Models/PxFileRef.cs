namespace PxApi.Models
{
    public record struct PxFileRef
    {
        public string Id { get; init; }
        public DataBaseRef DataBase { get; init; }

        private PxFileRef(string id, DataBaseRef dataBase)
        {
            Id = id;
            DataBase = dataBase;
        }

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

        public readonly override int GetHashCode()
        {
            return HashCode.Combine(Id, DataBase);
        }
    }
}
