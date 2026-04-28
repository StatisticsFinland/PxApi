using Microsoft.AspNetCore.Mvc;
using PxApi.Authentication;
using PxApi.Configuration;
using PxApi.DataSources;
using PxApi.Models;
using PxApi.Services;

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
        /// Returns 200 OK if the application is alive and all database connections and enabled services are healthy.
        /// Returns 503 Service Unavailable if any database connection or enabled service is unhealthy.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Health status with details about each database connection and enabled services.</returns>
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
                    databaseStatuses.Add(new DatabaseHealthStatus(dbId, HealthStatus.Healthy));
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Database {DatabaseId} health check failed", dbId);
                    databaseStatuses.Add(new DatabaseHealthStatus(dbId, HealthStatus.Unhealthy));
                    allHealthy = false;
                }
            }

            HealthResponse response = new(allHealthy ? HealthStatus.Healthy : HealthStatus.Unhealthy, databaseStatuses);

            if (AppSettings.Active.Features.SearchController)
            {
                SearchHealthStatus searchStatus = await CheckSearchHealthAsync(ct);
                if (searchStatus.Status != HealthStatus.Healthy)
                {
                    allHealthy = false;
                }
                response = response with
                {
                    Status = allHealthy ? HealthStatus.Healthy : HealthStatus.Unhealthy,
                    Search = searchStatus
                };
            }

            if (allHealthy)
            {
                return Ok(response);
            }

            return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
        }

        private async Task<SearchHealthStatus> CheckSearchHealthAsync(CancellationToken ct)
        {
            try
            {
                using IServiceScope scope = serviceProvider.CreateScope();
                ISearchService searchService = scope.ServiceProvider.GetRequiredService<ISearchService>();
                await searchService.CheckHealthAsync(ct);
                logger.LogDebug("Search backend health check passed");
                return new SearchHealthStatus(HealthStatus.Healthy);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Search backend health check failed");
                return new SearchHealthStatus(HealthStatus.Unhealthy);
            }
        }
    }
}
