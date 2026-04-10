using Microsoft.AspNetCore.Mvc;
using PxApi.Authentication;
using PxApi.Configuration;
using PxApi.DataSources;
using PxApi.Models;
using PxApi.Utilities;

namespace PxApi.Controllers
{
    /// <summary>
    /// Provides a health check endpoint that validates the application is running
    /// and all configured database connections are functional.
    /// </summary>
    [ApiKeyAuth]
    [ApiController]
    [Route("health")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class HealthController(IServiceProvider serviceProvider, ILogger<HealthController> logger) : ControllerBase
    {
        /// <summary>
        /// Returns 200 OK if the application is alive and all database connections are healthy.
        /// Returns 503 Service Unavailable if any database connection fails.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Health status with details about each database connection.</returns>
        [HttpGet]
        [Produces("application/json")]
        [ProducesResponseType(typeof(HealthResponse), 200)]
        [ProducesResponseType(typeof(HealthResponse), 503)]
        public async Task<IActionResult> GetHealthAsync(CancellationToken ct)
        {
            List<DatabaseHealthStatus> databaseStatuses = [];
            bool allHealthy = true;

            foreach (DataBaseConfig dbConfig in AppSettings.Active.DataBases)
            {
                string dbId = dbConfig.Id;
                try
                {
                    using IServiceScope scope = serviceProvider.CreateScope();
                    IDataBaseConnector connector = scope.ServiceProvider.GetRequiredKeyedService<IDataBaseConnector>(dbId);
                    await connector.CheckConnectionAsync(ct);

                    logger.LogDebug("Database {DatabaseId} health check passed", dbId);
                    databaseStatuses.Add(new DatabaseHealthStatus(dbId, "healthy"));
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Database {DatabaseId} health check failed", dbId);
                    databaseStatuses.Add(new DatabaseHealthStatus(dbId, "unhealthy"));
                    allHealthy = false;
                }
            }

            HealthResponse response = new(allHealthy ? "healthy" : "unhealthy", databaseStatuses);

            if (allHealthy)
            {
                return Ok(response);
            }

            return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
        }
    }
}
