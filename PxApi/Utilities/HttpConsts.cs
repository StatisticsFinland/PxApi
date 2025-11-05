namespace PxApi.Utilities
{
    /// <summary>
    /// Constants to keep HTTP responses standardized.
    /// </summary>
    public static class HttpConsts
    {
        /// <summary>
        /// Use to indicate in http responses that an internal server error has occurred.
        /// </summary>
        public const string INTERNAL_SERVER_ERROR = "An internal server error occurred.";

        /// <summary>
        /// Use to indicate in http responses that the request parameters are invalid.
        /// </summary>
        public const string BAD_REQUEST_PARAMS = "The request parameters are invalid.";
    }
}
