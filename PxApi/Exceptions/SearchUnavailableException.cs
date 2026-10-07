namespace PxApi.Exceptions
{
    /// <summary>
    /// Exception thrown when the Elasticsearch search backend is unreachable or returns an error.
    /// </summary>
    /// <param name="message">A diagnostic description without backend request or response bodies.</param>
    /// <param name="innerException">The underlying transport failure, when available.</param>
    /// <param name="statusCode">The backend HTTP status, when available.</param>
    /// <param name="errorType">The bounded, sanitized backend error category, when available.</param>
    public class SearchUnavailableException(string message, Exception? innerException, int? statusCode, string? errorType)
        : Exception(message, innerException)
    {
        /// <summary>Gets the backend HTTP status, when available.</summary>
        public int? StatusCode { get; } = statusCode;

        /// <summary>Gets the bounded, sanitized backend error category, when available.</summary>
        public string? ErrorType { get; } = errorType;

        /// <summary>Initializes a search failure without backend response context.</summary>
        /// <param name="message">The diagnostic description.</param>
        /// <param name="innerException">The underlying failure, when available.</param>
        public SearchUnavailableException(string message, Exception? innerException = null)
            : this(message, innerException, null, null)
        {
        }
    }
}
