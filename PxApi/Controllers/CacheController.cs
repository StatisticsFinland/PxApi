using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement.Mvc;
using PxApi.Authentication;
using PxApi.Caching;
using PxApi.Models;
using PxApi.Utilities;
using System.Collections.Immutable;
using PxApi.OpenApi;

namespace PxApi.Controllers
{
    /// <summary>
    /// Controller for managing API caches.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="CacheController"/> class.
    /// </remarks>
    /// <param name="cachedConnector">Cache connector for accessing cache operations.</param>
    /// <param name="logger">Logger for logging information, warnings and errors.</param>
    [ApiKeyAuth]
    [FeatureGate(nameof(CacheController))]
    [Route("cache")]
    [ApiController]
    public class CacheController(ICachedDataSource cachedConnector, ILogger<CacheController> logger) : ControllerBase
    {
        private readonly ICachedDataSource _cachedConnector = cachedConnector;
        private readonly ILogger<CacheController> _logger = logger;

        private const string DB_NOT_FOUND = "Database not found";

        /// <summary>
        /// Clears data, metadata and last updated caches related to a specific px file in a specific database.
        /// </summary>
        /// <param name="database">Name of the database with the table cache to clear</param>
        /// <param name="id">Id of the px file to be cleared</param>
        [HttpDelete("{database}/{id}")]
        [OperationId("clearTableCache")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(typeof(object), 401)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        public async Task<ActionResult> ClearTableCacheAsync([FromRoute] string database, string id)
        {
            using (_logger.BeginScope(new Dictionary<string, string>
            {
                { LoggerConsts.CONTROLLER, nameof(CacheController) },
                { LoggerConsts.FUNCTION, nameof(ClearTableCacheAsync) },
                { LoggerConsts.DB_ID, database },
                { LoggerConsts.PX_FILE, id }
            }))
            {
                DataBaseRef? dbRef = _cachedConnector.GetDataBaseReference(database);
                if (dbRef == null)
                {
                    _logger.LogWarning(DB_NOT_FOUND);
                    return NotFound(new { message = DB_NOT_FOUND });
                }

                try
                {
                    ImmutableSortedDictionary<string, PxFileRef> files = await _cachedConnector.GetFileListCachedAsync(dbRef.Value);
                    if (!files.TryGetValue(id, out PxFileRef pxFileRef))
                    {
                        _logger.LogWarning("PX file not found in database");
                        return NotFound(new { message = $"PX file not found in database" });
                    }
                    _cachedConnector.ClearTableCache(pxFileRef);
                    _logger.LogInformation("Cache for PX file cleared successfully");
                    return Ok(new { message = $"Cache for PX file cleared successfully" });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error clearing cache for PX file.");
                    return StatusCode(500, new { message = $"Error clearing cache for PX file", error = ex.Message });
                }
            }
        }

        /// <summary>
        /// Clears all cache entries for a specific database.
        /// </summary>
        /// <param name="database">Name of the database for which to clear all cache entries.</param>
        /// <returns>A message indicating the result of the operation.</returns>
        /// <response code="200">All cache entries were successfully cleared.</response>
        /// <response code="401">If the API key authentication fails.</response>
        /// <response code="404">If the database was not found.</response>
        /// <response code="500">If an error occurs while clearing cache entries.</response>
        [HttpDelete("{database}")]
        [OperationId("clearDatabaseCache")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(typeof(object), 401)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        public async Task<ActionResult> ClearAllCacheAsync([FromRoute] string database)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                { LoggerConsts.CONTROLLER, nameof(CacheController) },
                { LoggerConsts.FUNCTION, nameof(ClearAllCacheAsync) },
                { LoggerConsts.DB_ID, database }
            }))
            {
                DataBaseRef? dbRef = _cachedConnector.GetDataBaseReference(database);
                if (dbRef == null)
                {
                    _logger.LogWarning(DB_NOT_FOUND);
                    return NotFound(new { message = DB_NOT_FOUND });
                }

                try
                {
                    await _cachedConnector.ClearDatabaseCacheAsync(dbRef.Value);
                    _logger.LogInformation("All cache entries for database {DatabaseId} cleared successfully", dbRef.Value.Id);
                    return Ok(new { message = $"All cache entries for database '{dbRef.Value.Id}' cleared successfully" });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error clearing all cache entries for database {DatabaseId}", dbRef.Value.Id);
                    return StatusCode(500, new { message = $"Error clearing all cache entries for database '{dbRef.Value.Id}'", error = ex.Message });
                }
            }
        }
    }
}