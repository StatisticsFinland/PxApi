using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PxApi.Services;

namespace PxApi.Filters
{
    /// <summary>
    /// Global exception filter that handles <see cref="OperationCanceledException"/> (and its subclass
    /// <see cref="TaskCanceledException"/>) thrown during request processing.
    /// Returns HTTP 499; cancellation cause is recorded by request completion only when known.
    /// Routine cancellation diagnostics are emitted only for untracked requests.
    /// </summary>
    public class OperationCanceledExceptionFilter(ILogger<OperationCanceledExceptionFilter> logger) : IExceptionFilter
    {
        /// <inheritdoc />
        public void OnException(ExceptionContext context)
        {
            if (context.Exception is OperationCanceledException)
            {
                if (QueryObservation.Get(context.HttpContext) is null)
                    logger.LogDebug("Request was cancelled.");
                context.Result = new StatusCodeResult(499);
                context.ExceptionHandled = true;
            }
        }
    }
}
