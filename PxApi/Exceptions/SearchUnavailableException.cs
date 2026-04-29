namespace PxApi.Exceptions
{
    /// <summary>
    /// Exception thrown when the Elasticsearch search backend is unreachable or returns an error.
    /// </summary>
    public class SearchUnavailableException(string message, Exception? innerException = null) : Exception(message, innerException);
}
