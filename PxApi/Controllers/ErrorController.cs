using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace PxApi.Controllers
{
    /// <summary>
    /// Controller for /error endpoint.
    /// Used to handle unexpected errors in the API.
    /// </summary>
    /// <param name="logger">Logger</param>
    [ApiController]
    [Route("error")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class ErrorController(ILogger<ErrorController> logger) : ControllerBase
    {
        /// <summary>
        /// Handle unexpected errors in the API.
        /// If the endpoint is requested derectly, will return <see cref="StatusCodes.Status200OK"/>
        /// If an error occurred, will log the error and return <see cref="StatusCodes.Status400BadRequest"/>
        /// </summary>
        /// <returns><see cref="StatusCodes.Status200OK"/> or <see cref="StatusCodes.Status400BadRequest"/> depending how it was called.</returns>
        /// <response code="200">If the endpoint was called directly</response>
        /// <response code="400">If error occured in some other controller, the error has been logged.</response>
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
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
