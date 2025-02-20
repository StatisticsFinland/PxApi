using System.Text.Json;

namespace PxApi.Configuration
{
    /// <summary>
    /// Global options for JSON serialization and deserialization.
    /// Forces the same options to be used throughout the application.
    /// </summary>
    public static class GlobalJsonConverterOptions
    {
        /// <summary>
        /// Default options for JSON serialization and deserialization.
        /// </summary>
        public static JsonSerializerOptions Default { get; set; } = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
    }
}
