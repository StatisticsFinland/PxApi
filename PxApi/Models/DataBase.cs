using Px.Utils.Language;

namespace PxApi.Models
{
    public class DataBaseMeta
    {
        public required string Id { get; init; }

        public required MultilanguageString Name { get; init; }

        public required MultilanguageString Description { get; init; }
    }
}
