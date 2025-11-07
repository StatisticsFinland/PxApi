using Microsoft.Net.Http.Headers;

namespace PxApi.Utilities
{
    /// <summary>
    /// Provides utilities for HTTP content negotiation based on Accept headers with quality values.
    /// </summary>
    public static class ContentNegotiation
    {
        /// <summary>
        /// Determines the best matching media type from the Accept header based on quality values (q-values) 
        /// according to RFC 9110 specifications.
        /// </summary>
        /// <param name="acceptHeaderValues">Collection of media types from the Accept header with quality values.</param>
        /// <param name="supportedMediaTypes">Array of media types supported by the endpoint.</param>
        /// <returns>The best matching supported media type, or null if no match is found.</returns>
        public static string? GetBestMatch(IList<MediaTypeHeaderValue> acceptHeaderValues, string[] supportedMediaTypes)
        {
            if (acceptHeaderValues.Count == 0)
            {
                return null;
            }

            // Create a list of matches with their quality values
            List<(string MediaType, double Quality)> matches = [];

            foreach (string supportedType in supportedMediaTypes)
            {
                foreach (MediaTypeHeaderValue acceptValue in acceptHeaderValues)
                {
                    if (acceptValue.MediaType.Value != null && IsMediaTypeMatch(acceptValue.MediaType.Value, supportedType))
                    {
                        // Quality defaults to 1.0 if not specified
                        double quality = acceptValue.Quality ?? 1.0;
                        matches.Add((supportedType, quality));
                    }
                }
            }

            // Return the media type with the highest quality value
            // If multiple have the same quality, prefer the first one (respects order in supportedMediaTypes)
            return matches
                .OrderByDescending(match => match.Quality)
                .ThenBy(match => Array.IndexOf(supportedMediaTypes, match.MediaType))
                .FirstOrDefault().MediaType;
        }

        /// <summary>
        /// Checks if an Accept header media type matches a supported media type.
        /// Handles wildcards (*/*) and subtype wildcards (text/*).
        /// </summary>
        /// <param name="acceptMediaType">Media type from Accept header (may contain wildcards).</param>
        /// <param name="supportedMediaType">Specific media type supported by the endpoint.</param>
        /// <returns>True if the media types match, false otherwise.</returns>
        private static bool IsMediaTypeMatch(string acceptMediaType, string supportedMediaType)
        {
            if (string.Equals(acceptMediaType, supportedMediaType, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Handle */* wildcard
            if (acceptMediaType == "*/*")
            {
                return true;
            }

            // Handle type/* wildcards (e.g., text/*)
            if (acceptMediaType.EndsWith("/*", StringComparison.OrdinalIgnoreCase))
            {
                string acceptType = acceptMediaType[..^2]; // Remove "/*"
                string[] supportedParts = supportedMediaType.Split('/');
                
                if (supportedParts.Length == 2)
                {
                    return string.Equals(acceptType, supportedParts[0], StringComparison.OrdinalIgnoreCase);
                }
            }

            return false;
        }
    }
}