using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PxApi.Configuration;
using PxApi.Controllers;
using PxApi.Utilities;

namespace PxApi.Authentication
{
    /// <summary>
    /// Attribute that enforces API key authentication on controller actions.
    /// Authentication is only applied if API key authentication is configured.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class ApiKeyAuthAttribute : Attribute, IAsyncActionFilter
    {
        /// <summary>
        /// Executes the authentication logic before the action method.
        /// </summary>
        /// <param name="context">The action executing context.</param>
        /// <param name="next">The action execution delegate.</param>
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            ILogger<ApiKeyAuthAttribute> logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<ApiKeyAuthAttribute>>();
            
            using (logger.BeginScope(new Dictionary<string, object>
            {
                { LoggerConsts.CONTROLLER, nameof(ApiKeyAuthAttribute) },
                { LoggerConsts.FUNCTION, nameof(OnActionExecutionAsync) }
            }))
            {
                // Check if authentication is configured
                if (!AppSettings.Active.Authentication.IsEnabled)
                {
                    logger.LogDebug("Authentication is not configured, allowing request to proceed");
                    await next();
                    return;
                }

                // Determine which controller is being called and get the appropriate config
                ApiKeyConfig? apiKeyConfig = GetApiKeyConfigForController(context);
                if (apiKeyConfig is null || !apiKeyConfig.IsEnabled)
                {
                    logger.LogDebug("API key authentication is not enabled for this controller, allowing request to proceed");
                    await next();
                    return;
                }

                // Extract API key from request headers
                if (!context.HttpContext.Request.Headers.TryGetValue(apiKeyConfig.HeaderName, out Microsoft.Extensions.Primitives.StringValues potentialApiKey))
                {
                    logger.LogWarning("API key authentication failed: Missing {HeaderName} header", apiKeyConfig.HeaderName);
                    context.Result = new UnauthorizedObjectResult(new { message = $"Missing {apiKeyConfig.HeaderName} header" });
                    return;
                }

                string providedKey = potentialApiKey.ToString();
                if (string.IsNullOrEmpty(providedKey))
                {
                    logger.LogWarning("API key authentication failed: Empty API key provided");
                    context.Result = new UnauthorizedObjectResult(new { message = "Invalid API key" });
                    return;
                }

                // Compare provided key directly with configured key
                if (!string.Equals(providedKey, apiKeyConfig.Key, StringComparison.Ordinal))
                {
                    logger.LogWarning("API key authentication failed: Invalid API key provided");
                    context.Result = new UnauthorizedObjectResult(new { message = "Invalid API key" });
                    return;
                }
                
                logger.LogDebug("API key authentication successful");
                await next();
            }
        }
        
        /// <summary>
        /// Determines which API key configuration to use based on the controller being called.
        /// </summary>
        /// <param name="context">The action executing context.</param>
        /// <returns>The appropriate API key configuration, or null if no matching controller is found.</returns>
        private static ApiKeyConfig? GetApiKeyConfigForController(ActionExecutingContext context)
        {
            string controllerName = context.Controller.GetType().Name;
            AuthenticationConfig authConfig = AppSettings.Active.Authentication;
            
            return controllerName switch
            {
                nameof(DatabasesController) => authConfig.Databases,
                nameof(TablesController) => authConfig.Tables,
                nameof(MetadataController) => authConfig.Metadata,
                nameof(DataController) => authConfig.Data,
                nameof(CacheController) => authConfig.Cache,
                _ => null
            };
        }
    }
}