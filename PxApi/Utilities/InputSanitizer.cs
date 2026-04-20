using System.Text.RegularExpressions;

namespace PxApi.Utilities
{
    /// <summary>
    /// Provides methods for sanitizing user input before logging or further processing.
    /// </summary>
    internal static partial class InputSanitizer
    {
        /// <summary>
        /// Replaces control characters and trims the input to <paramref name="maxLength"/>
        /// so that user-supplied text is safe for structured logging (prevents log injection).
        /// </summary>
        internal static string SanitizeForLog(string? input, int maxLength = 200)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            string trimmed = input.Length > maxLength ? input[..maxLength] : input;
            return ControlCharPattern().Replace(trimmed, " ");
        }

        [GeneratedRegex(@"[\p{C}]")]
        private static partial Regex ControlCharPattern();
    }
}
