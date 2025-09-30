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
        /// Mappping of the dimensions and their selected values.
        /// </summary>
        [Required]
        public required MatrixMap MetaCodes { get; init; }

        /// <summary>
        /// Array of data values returned by the query.
        /// </summary>
        [Required]
        public required DoubleDataValue[] Data { get; init; }
    }
}
