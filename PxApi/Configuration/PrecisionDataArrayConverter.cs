using Px.Utils.Models.Data;
using Px.Utils.Models.Data.DataValue;
using PxApi.Models.JsonStat;
using PxApi.Utilities;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PxApi.Configuration
{
    /// <summary>
    /// Custom JSON converter for <see cref="PrecisionDataArray"/>.
    /// Writes the data values as a JSON array, applying per-cell decimal precision
    /// from the <see cref="PrecisionDataArray.Precision"/> resolver when available.
    /// </summary>
    public class PrecisionDataArrayConverter : JsonConverter<PrecisionDataArray>
    {
        /// <summary>
        /// Not supported. This converter only supports writing <see cref="PrecisionDataArray"/> to JSON format.
        /// Reading from JSON is not implemented.
        /// </summary>
        /// <exception cref="NotSupportedException">Always thrown as this operation is not supported.</exception>
        public override PrecisionDataArray Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, PrecisionDataArray value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();

            for (int i = 0; i < value.Length; i++)
            {
                DoubleDataValue dataValue = value[i];

                if (dataValue.Type == DataValueType.Exists)
                {
                    int precision = value.Precision?.Resolve(i) ?? PrecisionResolver.NoPrecision;
                    if (precision == PrecisionResolver.NoPrecision)
                    {
                        writer.WriteNumberValue(dataValue.UnsafeValue);
                    }
                    else
                    {
                        double rounded = Math.Round(dataValue.UnsafeValue, precision, MidpointRounding.AwayFromZero);
                        writer.WriteRawValue(rounded.ToString(PrecisionResolver.FormatStrings[precision], CultureInfo.InvariantCulture));
                    }
                }
                else
                {
                    writer.WriteNullValue();
                }
            }

            writer.WriteEndArray();
        }
    }
}
