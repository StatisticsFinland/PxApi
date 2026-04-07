using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PxApi.Filters
{
    /// <summary>
    /// Global exception filter that handles <see cref="OperationCanceledException"/> (and its subclass
    /// <see cref="TaskCanceledException"/>) thrown when a client disconnects during request processing.
    /// Returns HTTP 499 (Client Closed Request) and logs at debug level to avoid polluting error logs.
    /// </summary>
    public class OperationCancelledExceptionFilter(ILogger<OperationCancelledExceptionFilter> logger) : IExceptionFilter
    {
        /// <inheritdoc />
        public void OnException(ExceptionContext context)
        {
            if (context.Exception is OperationCanceledException)
            {
                logger.LogDebug("Request was cancelled for {Path}.", context.HttpContext.Request.Path);
                context.Result = new StatusCodeResult(499);
                context.ExceptionHandled = true;
            }
        }
    }
}
