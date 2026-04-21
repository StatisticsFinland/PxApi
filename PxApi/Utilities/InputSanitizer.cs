using System.Text.RegularExpressions;

namespace PxApi.Utilities
{
    /// <summary>
    /// Provides methods for sanitizing user input before logging or further processing.
    /// </summary>
    internal static partial class InputSanitizer
    {
        /// <summary>
        /// Removes characters that are not considered normal human-readable text and trims the
        /// input to <paramref name="maxLength"/> using a whitelist-based approach.
        /// </summary>
        internal static string SanitizeInput(string? input, int maxLength = 200)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            string trimmed = input.Length > maxLength ? input[..maxLength] : input;
            return DisallowedCharPattern().Replace(trimmed, string.Empty);
        }

        [GeneratedRegex(@"[^\p{L}\p{N} .,:!?'_-]")]
        private static partial Regex DisallowedCharPattern();
    }
}
