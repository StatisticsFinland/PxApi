using System.Text.Json;
using PxApi.Models.QueryFilters;

namespace PxApi.Configuration
{
    /// <summary>
    /// Global options for JSON serialization and deserialization.
    /// Forces the same options to be used throughout the application.
    /// </summary>
    public static class GlobalJsonConverterOptions
    {
        static GlobalJsonConverterOptions()
        {
            // Create the default options
            Default = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            // Add the Filter converter and DataValueType converter
            Default.Converters.Add(new FilterJsonConverter());
            Default.Converters.Add(new DataValueTypeJsonConverter());
        }

        /// <summary>
        /// Default options for JSON serialization and deserialization.
        /// </summary>
        public static JsonSerializerOptions Default { get; }
    }
}
