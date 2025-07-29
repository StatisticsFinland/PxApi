namespace PxApi.Models
{
    public record struct DataBaseRef
    {
        public string Id { get; init; }

        private DataBaseRef(string id)
        {
            Id = id;
        }

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

        public override readonly int GetHashCode() => Id.GetHashCode();
    }
}
