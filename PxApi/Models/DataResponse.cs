using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Metadata;

namespace PxApi.Models
{
    public class DataResponse
    {
        public required DateTime LastUpdated { get; init; }

        public required MatrixMap MetaCodes { get; init; }

        public required DoubleDataValue[] Data { get; init; }
    }
}
