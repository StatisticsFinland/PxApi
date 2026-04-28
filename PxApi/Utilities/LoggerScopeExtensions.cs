namespace PxApi.Utilities
{
    /// <summary>
    /// Extension methods for <see cref="ILogger"/> that simplify creating common logging scopes
    /// used across controllers and services.
    /// </summary>
    public static class LoggerScopeExtensions
    {
        /// <summary>
        /// Begins a logging scope containing a database identifier.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="dbId">The database identifier to include in the scope.</param>
        /// <returns>A disposable scope token.</returns>
        public static IDisposable? BeginDbScope(this ILogger logger, string dbId)
        {
            return logger.BeginScope(new Dictionary<string, object>
            {
                { LoggerConsts.DB_ID, dbId }
            });
        }

        /// <summary>
        /// Begins a logging scope indicating the requested database was not found.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <returns>A disposable scope token.</returns>
        public static IDisposable? BeginDbNotFoundScope(this ILogger logger)
        {
            return logger.BeginDbScope(LoggerConsts.NOT_FOUND_PLACEHOLDER);
        }

        /// <summary>
        /// Begins a logging scope containing a PX file identifier.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="fileId">The PX file identifier to include in the scope.</param>
        /// <returns>A disposable scope token.</returns>
        public static IDisposable? BeginFileScope(this ILogger logger, string fileId)
        {
            return logger.BeginScope(new Dictionary<string, object>
            {
                { LoggerConsts.PX_FILE, fileId }
            });
        }

        /// <summary>
        /// Begins a logging scope containing both a database identifier and a PX file identifier.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="dbId">The database identifier.</param>
        /// <param name="fileId">The PX file identifier.</param>
        /// <returns>A disposable scope token.</returns>
        public static IDisposable? BeginResourceScope(this ILogger logger, string dbId, string fileId)
        {
            return logger.BeginScope(new Dictionary<string, object>
            {
                { LoggerConsts.DB_ID, dbId },
                { LoggerConsts.PX_FILE, fileId }
            });
        }

        /// <summary>
        /// Begins a logging scope indicating the requested resource (database and/or PX file) was not found.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="dbId">The database identifier, or <c>null</c> to use the not-found placeholder for both fields.</param>
        /// <returns>A disposable scope token.</returns>
        public static IDisposable? BeginResourceNotFoundScope(this ILogger logger, string? dbId = null)
        {
            return logger.BeginResourceScope(
                dbId ?? LoggerConsts.NOT_FOUND_PLACEHOLDER,
                LoggerConsts.NOT_FOUND_PLACEHOLDER);
        }

        /// <summary>
        /// Begins a logging scope containing a search query and, optionally, a database identifier.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="query">The search query to include in the scope.</param>
        /// <param name="dbId">Optional database identifier to include in the scope.</param>
        /// <returns>A disposable scope token.</returns>
        public static IDisposable? BeginSearchScope(this ILogger logger, string? query, string? dbId = null)
        {
            Dictionary<string, object> scopeValues = new()
            {
                { LoggerConsts.SEARCH_QUERY, query ?? string.Empty }
            };

            if (!string.IsNullOrWhiteSpace(dbId))
            {
                scopeValues[LoggerConsts.DB_ID] = dbId;
            }

            return logger.BeginScope(scopeValues);
        }
    }
}
