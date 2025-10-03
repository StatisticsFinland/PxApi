using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Metadata;
using System.ComponentModel.DataAnnotations;

namespace PxApi.Models
{
    /// <summary>
    /// Response model for data queries.
    /// </summary>
    public class DataResponse
    {
        /// <summary>
        /// Date and time when the px table was last updated.
        /// </summary>
        [Required]
        public required DateTime LastUpdated { get; init; }

        /// <summary>
        /// Mappping of the dimensions and their selected values. These dimension define the data in a row-major order.
        /// </summary>
        [Required]
        public required List<DimensionMap> Dimensions { get; init; }

        /// <summary>
        /// Array of data values returned by the query.
        /// </summary>
        [Required]
        public required DoubleDataValue[] Data { get; init; }
    }
}
