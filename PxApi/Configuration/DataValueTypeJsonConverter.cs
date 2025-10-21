using Px.Utils.Models.Data;
using Px.Utils.Models.Data.DataValue;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PxApi.Configuration
{
    /// <summary>
    /// Custom JSON converter for the DataValueType enum.
    /// This converter serializes the enum as its underlying byte value.
    /// </summary>
    public class DataValueTypeJsonConverter : JsonConverter<DataValueType>
    {
        /// <summary>
        /// Reads and converts the JSON to type <see cref="DataValueType"/>.
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="typeToConvert">The type to convert.</param>
        /// <param name="options">An object that specifies serialization options to use.</param>
        /// <returns>The converted value.</returns>
        public override DataValueType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                return (DataValueType)reader.GetByte();
            }
            throw new JsonException("Expected a byte value for DataValueType.");
        }

        /// <summary>
        /// Reads a dictionary key from a JSON property name.
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="typeToConvert">The type to convert.</param>
        /// <param name="options">An object that specifies serialization options to use.</param>
        /// <returns>The converted value.</returns>
        public override DataValueType ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return (DataValueType)byte.Parse(reader.GetString()!, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Writes a specified value as JSON.
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="value">The value to convert to JSON.</param>
        /// <param name="options">An object that specifies serialization options to use.</param>
        public override void Write(Utf8JsonWriter writer, DataValueType value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue((byte)value);
        }

        /// <summary>
        /// Writes a dictionary key as a JSON property name.
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="value">The value to convert to JSON.</param>
        /// <param name="options">An object that specifies serialization options to use.</param>
        public override void WriteAsPropertyName(Utf8JsonWriter writer, DataValueType value, JsonSerializerOptions options)
        {
            writer.WritePropertyName(((byte)value).ToString(CultureInfo.InvariantCulture));
        }
    }
}
