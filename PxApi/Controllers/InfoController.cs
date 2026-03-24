using Microsoft.AspNetCore.Mvc;
using PxApi.Models;
using System.Reflection;

namespace PxApi.Controllers
{
    /// <summary>
    /// Provides an info endpoint that returns application metadata
    /// such as the current version, useful for validating connectivity.
    /// </summary>
    [ApiController]
    [Route("info")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class InfoController : ControllerBase
    {
        /// <summary>
        /// Returns basic application information including the version.
        /// Can be used to validate connectivity to the application.
        /// </summary>
        /// <returns>Application information including the version.</returns>
        [HttpGet]
        [Produces("application/json")]
        [ProducesResponseType(typeof(InfoResponse), 200)]
        public IActionResult GetInfo()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString()
                ?? "unknown";

            InfoResponse response = new("PxApi", version);
            return Ok(response);
        }
    }
}
