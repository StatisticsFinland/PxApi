using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using PxApi.Exceptions;

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
        /// If the endpoint is requested directly, will return <see cref="StatusCodes.Status200OK"/>
        /// If an error occurred, will log the error and return appropriate status code based on error type.
        /// </summary>
        /// <returns>Appropriate HTTP status code based on the error type.</returns>
        /// <response code="200">If the endpoint was called directly</response>
        /// <response code="400">If a client error occurred (e.g., malformed JSON or invalid model)</response>
        /// <response code="500">If an internal server error occurred</response>
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public IActionResult Error()
        {
            IExceptionHandlerPathFeature? exceptionHandlerPathFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            if (exceptionHandlerPathFeature?.Error == null)
            {
                // No actual exception? User may have requested error path manually
                logger.LogError("Error handler requested without exception.");
                return Ok();
            }

            Exception exception = exceptionHandlerPathFeature.Error;

            if (exception is InvalidModelException modelEx)
            {
                logger.LogInformation(modelEx, "Invalid model exception occurred.");
                return BadRequest("The request model is invalid.");
            }
            else if (exception is JsonException jsonEx)
            {
                logger.LogInformation(jsonEx, "Malformed JSON exception occurred.");
                return BadRequest("The request contains malformed JSON.");
            }
            // Handle IO errors with critical logging
            else if (exception is IOException)
            {
                logger.LogCritical(exception, "An IO error occurred.");
                return StatusCode(500, "An error occurred while processing the request.");
            }

            // Handle other exceptions
            logger.LogError(exception, "An unexpected error occurred.");
            return StatusCode(500, "An error occurred while processing the request.");
        }
    }
}
