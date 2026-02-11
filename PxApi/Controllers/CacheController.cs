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
        /// <response code="200">Cache for the specified PX file was successfully cleared.</response>
        /// <response code="404">If the database or PX file was not found.</response>
        /// <response code="500">If an error occurs while clearing the cache.</response>
        [HttpDelete("{database}/{id}")]
        [OperationId("clearTableCache")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(string), 200)]
        [ProducesResponseType(typeof(string), 404)]
        [ProducesResponseType(typeof(string), 500)]
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
                    return NotFound(DB_NOT_FOUND);
                }

                try
                {
                    ImmutableSortedDictionary<string, PxFileRef> files = await _cachedConnector.GetFileListCachedAsync(dbRef.Value);
                    if (!files.TryGetValue(id, out PxFileRef pxFileRef))
                    {
                        _logger.LogWarning("PX file not found in database");
                        return NotFound("PX file not found in database");
                    }
                    _cachedConnector.ClearTableCache(pxFileRef);
                    _logger.LogInformation("Cache for PX file cleared successfully");
                    return Ok("Cache for PX file cleared successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error clearing cache for PX file: {Message}", ex.Message);
                    return StatusCode(500, "Error clearing cache for PX file");
                }
            }
        }

        /// <summary>
        /// Clears all cache entries for a specific database.
        /// </summary>
        /// <param name="database">Name of the database for which to clear all cache entries.</param>
        /// <returns>A message indicating the result of the operation.</returns>
        /// <response code="200">All cache entries were successfully cleared.</response>
        /// <response code="404">If the database was not found.</response>
        /// <response code="500">If an error occurs while clearing cache entries.</response>
        [HttpDelete("{database}")]
        [OperationId("clearDatabaseCache")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(string), 200)]
        [ProducesResponseType(typeof(string), 404)]
        [ProducesResponseType(typeof(string), 500)]
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
                    return NotFound(DB_NOT_FOUND);
                }

                try
                {
                    await _cachedConnector.ClearDatabaseCacheAsync(dbRef.Value);
                    _logger.LogInformation("All cache entries for database {DatabaseId} cleared successfully", dbRef.Value.Id);
                    return Ok($"All cache entries for database '{dbRef.Value.Id}' cleared successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error clearing all cache entries for database {DatabaseId}: {Message}", dbRef.Value.Id, ex.Message);
                    return StatusCode(500, $"Error clearing all cache entries for database '{dbRef.Value.Id}'");
                }
            }
        }
    }
}