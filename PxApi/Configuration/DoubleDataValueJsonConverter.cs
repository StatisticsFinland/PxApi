using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Data;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace PxApi.Configuration
{
    /// <summary>
    /// Custom JSON converter for DoubleDataValue structs from Px.Utils.
    /// </summary>
    public class DoubleDataValueJsonConverter : JsonConverter<DoubleDataValue>
    {
        /// <inheritdoc />
        public override DoubleDataValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return new DoubleDataValue(default, DataValueType.Missing);
            }

            if (reader.TokenType == JsonTokenType.Number)
            {
                return new DoubleDataValue(reader.GetDouble(), DataValueType.Exists);
            }

            throw new JsonException($"Cannot convert {reader.TokenType} to DoubleDataValue");
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, DoubleDataValue value, JsonSerializerOptions options)
        {
            if(value.Type == DataValueType.Exists)
            {
                writer.WriteNumberValue(value.UnsafeValue);
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}