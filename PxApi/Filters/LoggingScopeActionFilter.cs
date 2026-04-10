using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using PxApi.Utilities;

namespace PxApi.Filters
{
    /// <summary>
    /// Action filter that automatically pushes Controller and Action names into the logging scope
    /// for every controller action, eliminating the need for manual outer scope blocks in controllers.
    /// </summary>
    public class LoggingScopeActionFilter(ILogger<LoggingScopeActionFilter> logger) : IAsyncActionFilter
    {
        /// <inheritdoc />
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            string controllerName = context.Controller.GetType().Name;
            string actionName = context.ActionDescriptor is ControllerActionDescriptor descriptor
                ? descriptor.ActionName
                : context.ActionDescriptor.DisplayName ?? "Unknown";

            using (logger.BeginScope(new Dictionary<string, object>
            {
                { LoggerConsts.CONTROLLER, controllerName },
                { LoggerConsts.ACTION, actionName }
            }))
            {
                await next();
            }
        }
    }
}
