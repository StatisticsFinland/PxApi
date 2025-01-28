using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace PxApi.Controllers
{
    [ApiController]
    [Route("error")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class ErrorController(ILogger<ErrorController> logger) : ControllerBase
    {
        public IActionResult Error()
        {
            IExceptionHandlerPathFeature? exceptionHandlerPathFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            if (exceptionHandlerPathFeature?.Error == null)
            {
                // No actual exception? User may have requested error path manually
                logger.LogError("Error handler requested without exception.");
                return Ok();
            }
            if (exceptionHandlerPathFeature.Error is IOException)
            {
                logger.LogCritical(exceptionHandlerPathFeature.Error, "An IO error occurred in {Path}", exceptionHandlerPathFeature.Path);
            }
            else
            {
                logger.LogError(exceptionHandlerPathFeature.Error, "Error in {Path}", exceptionHandlerPathFeature.Path);
            }

            return BadRequest();
        }
    }
}
