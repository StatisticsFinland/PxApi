using Px.Utils.Models.Data.DataValue;
using PxApi.Configuration;
using PxApi.Utilities;
using System.Collections;
using System.Text.Json.Serialization;

namespace PxApi.Models.JsonStat
{
    /// <summary>
    /// Wraps a <see cref="DoubleDataValue"/> array together with an optional <see cref="PrecisionResolver"/>
    /// for per-cell decimal formatting during JSON serialization.
    /// Implements array-like access patterns so consumers can use indexing, Length, and LINQ.
    /// </summary>
    [JsonConverter(typeof(PrecisionDataArrayConverter))]
    public sealed class PrecisionDataArray : IEnumerable<DoubleDataValue>
    {
        /// <summary>
        /// The underlying data values.
        /// </summary>
        public DoubleDataValue[] Data { get; }

        /// <summary>
        /// Precision resolver for per-cell formatting. Null means full double precision.
        /// </summary>
        [JsonIgnore]
        public PrecisionResolver? Precision { get; init; }

        /// <summary>
        /// Gets the number of data values.
        /// </summary>
        public int Length => Data.Length;

        /// <summary>
        /// Gets the data value at the specified index.
        /// </summary>
        public DoubleDataValue this[int index] => Data[index];

        /// <summary>
        /// Initializes a new instance with the given data array.
        /// </summary>
        public PrecisionDataArray(DoubleDataValue[] data)
        {
            Data = data;
        }

        /// <summary>
        /// Initializes a new instance with the given data array and precision resolver.
        /// </summary>
        public PrecisionDataArray(DoubleDataValue[] data, PrecisionResolver precision)
        {
            Data = data;
            Precision = precision;
        }

        /// <summary>
        /// Implicit conversion from a <see cref="DoubleDataValue"/> array.
        /// </summary>
        public static implicit operator PrecisionDataArray(DoubleDataValue[] data) => new(data);

        /// <inheritdoc />
        public IEnumerator<DoubleDataValue> GetEnumerator() => ((IEnumerable<DoubleDataValue>)Data).GetEnumerator();

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => Data.GetEnumerator();
    }
}
