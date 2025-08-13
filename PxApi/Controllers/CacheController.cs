using Microsoft.AspNetCore.Mvc;
using PxApi.Caching;
using PxApi.DataSources;
using PxApi.Models;
using PxApi.Utilities;
using System.Collections.Immutable;

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
    [Route("cache")]
    [ApiController]
    public class CacheController(ICachedDataBaseConnector cachedConnector, ILogger<CacheController> logger) : ControllerBase
    {
        private readonly ICachedDataBaseConnector _cachedConnector = cachedConnector;
        private readonly ILogger<CacheController> _logger = logger;

        /// <summary>
        /// Clears the file list cache for all databases.
        /// </summary>
        /// <returns>A message indicating the result of the operation.</returns>
        /// <response code="200">File list cache was successfully cleared.</response>
        [HttpDelete("files")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(object), 200)]
        public ActionResult ClearFileListCache([FromRoute] string database)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                { LoggerConsts.CLASS_NAME, nameof(CacheController) },
                { LoggerConsts.METHOD_NAME, nameof(ClearFileListCache) },
                { LoggerConsts.DB_ID, database }
            }))
            {
                try
                {
                    DataBaseRef? dbRef = _cachedConnector.GetDataBaseReference(database);
                    if (dbRef == null)
                    {
                        _logger.LogWarning("Database not found");
                        return NotFound(new { message = "Database not found" });
                    }
                    _cachedConnector.ClearFileListCache(dbRef.Value);
                    _logger.LogInformation("File list cache cleared successfully");
                    return Ok(new { message = "File list cache cleared successfully" });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error clearing file list cache");
                    return StatusCode(500, new { message = "Error clearing file list cache", error = ex.Message });
                }
            }
        }

        /// <summary>
        /// Clears metadata cache for a specific database.
        /// </summary>
        /// <param name="database">Name of the database for which to clear metadata cache.</param>
        /// <returns>A message indicating the result of the operation.</returns>
        /// <response code="200">Metadata cache was successfully cleared.</response>
        /// <response code="404">If the database was not found.</response>
        [HttpDelete("{database}/metadata")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(404)]
        public ActionResult ClearMetadataCache([FromRoute] string database)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                { LoggerConsts.CLASS_NAME, nameof(CacheController) },
                { LoggerConsts.METHOD_NAME, nameof(ClearMetadataCache) },
                { LoggerConsts.DB_ID, database }
            }))
            {
                DataBaseRef? dbRef = _cachedConnector.GetDataBaseReference(database);
                if (dbRef == null)
                {
                    _logger.LogWarning("Database not found");
                    return NotFound(new { message = "Database not found" });
                }

                try
                {
                    _cachedConnector.ClearMetadataCacheAsync(dbRef.Value);
                    _logger.LogInformation("Metadata cache for database {DatabaseId} cleared successfully", dbRef.Value.Id);
                    return Ok(new { message = $"Metadata cache for database '{dbRef.Value.Id}' cleared successfully" });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error clearing metadata cache for database {DatabaseId}", dbRef.Value.Id);
                    return StatusCode(500, new { message = $"Error clearing metadata cache for database '{dbRef.Value.Id}'", error = ex.Message });
                }
            }
        }

        /// <summary>
        /// Clears data cache for a specific database.
        /// </summary>
        /// <param name="database">Name of the database for which to clear data cache.</param>
        /// <returns>A message indicating the result of the operation.</returns>
        /// <response code="200">Data cache was successfully cleared.</response>
        /// <response code="404">If the database was not found.</response>
        [HttpDelete("{database}/data")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(404)]
        public ActionResult ClearDataCache([FromRoute] string database)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                { LoggerConsts.CLASS_NAME, nameof(CacheController) },
                { LoggerConsts.METHOD_NAME, nameof(ClearDataCache) },
                { LoggerConsts.DB_ID, database }
            }))
            {
                DataBaseRef? dbRef = _cachedConnector.GetDataBaseReference(database);
                if (dbRef == null)
                {
                    _logger.LogWarning("Database not found");
                    return NotFound(new { message = "Database not found" });
                }

                try
                {
                    _cachedConnector.ClearDataCacheAsync(dbRef.Value);
                    _logger.LogInformation("Data cache for database {DatabaseId} cleared successfully", dbRef.Value.Id);
                    return Ok(new { message = $"Data cache for database '{dbRef.Value.Id}' cleared successfully" });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error clearing data cache for database {DatabaseId}", dbRef.Value.Id);
                    return StatusCode(500, new { message = $"Error clearing data cache for database '{dbRef.Value.Id}'", error = ex.Message });
                }
            }
        }

        /// <summary>
        /// Clears hierarchy cache for a specific database.
        /// </summary>
        /// <param name="database">Name of the database for which to clear hierarchy cache.</param>
        /// <returns>A message indicating the result of the operation.</returns>
        /// <response code="200">Hierarchy cache was successfully cleared.</response>
        /// <response code="404">If the database was not found.</response>
        [HttpDelete("{database}/hierarchy")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(404)]
        public ActionResult ClearHierarchyCache([FromRoute] string database)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                { LoggerConsts.CLASS_NAME, nameof(CacheController) },
                { LoggerConsts.METHOD_NAME, nameof(ClearHierarchyCache) },
                { LoggerConsts.DB_ID, database }
            }))
            {
                DataBaseRef? dbRef = _cachedConnector.GetDataBaseReference(database);
                if (dbRef == null)
                {
                    _logger.LogWarning("Database not found");
                    return NotFound(new { message = "Database not found" });
                }

                try
                {
                    _cachedConnector.ClearHierarchyCache(dbRef.Value);
                    _logger.LogInformation("Hierarchy cache for database {DatabaseId} cleared successfully", dbRef.Value.Id);
                    return Ok(new { message = $"Hierarchy cache for database '{dbRef.Value.Id}' cleared successfully" });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error clearing hierarchy cache for database {DatabaseId}", dbRef.Value.Id);
                    return StatusCode(500, new { message = $"Error clearing hierarchy cache for database '{dbRef.Value.Id}'", error = ex.Message });
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
        [HttpDelete("{database}")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(404)]
        public ActionResult ClearAllCache([FromRoute] string database)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                { LoggerConsts.CLASS_NAME, nameof(CacheController) },
                { LoggerConsts.METHOD_NAME, nameof(ClearAllCache) },
                { LoggerConsts.DB_ID, database }
            }))
            {
                DataBaseRef? dbRef = _cachedConnector.GetDataBaseReference(database);
                if (dbRef == null)
                {
                    _logger.LogWarning("Database not found");
                    return NotFound(new { message = "Database not found" });
                }

                try
                {
                    _cachedConnector.ClearAllCache(dbRef.Value);
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

        /// <summary>
        /// Clears metadata cache for all databases.
        /// </summary>
        /// <returns>A message indicating the result of the operation.</returns>
        /// <response code="200">All metadata caches were successfully cleared.</response>
        [HttpDelete("metadata")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(object), 200)]
        public async Task<ActionResult> ClearAllMetadataCaches()
        {
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                { LoggerConsts.CLASS_NAME, nameof(CacheController) },
                { LoggerConsts.METHOD_NAME, nameof(ClearAllMetadataCaches) }
            }))
            {
                try
                {
                    IReadOnlyCollection<DataBaseRef> allDatabases = _cachedConnector.GetAllDataBaseReferences();
                    foreach (DataBaseRef dbRef in allDatabases)
                    {
                        await _cachedConnector.ClearMetadataCacheAsync(dbRef);
                    }
                    _logger.LogInformation("All metadata caches cleared successfully");
                    return Ok(new { message = "All metadata caches cleared successfully" });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error clearing all metadata caches");
                    return StatusCode(500, new { message = "Error clearing all metadata caches", error = ex.Message });
                }
            }
        }

        /// <summary>
        /// Clears data cache for all databases.
        /// </summary>
        /// <returns>A message indicating the result of the operation.</returns>
        /// <response code="200">All data caches were successfully cleared.</response>
        [HttpDelete("data")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(object), 200)]
        public async Task<ActionResult> ClearAllDataCaches()
        {
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                { LoggerConsts.CLASS_NAME, nameof(CacheController) },
                { LoggerConsts.METHOD_NAME, nameof(ClearAllDataCaches) }
            }))
            {
                try
                {
                    IReadOnlyCollection<DataBaseRef> allDatabases = _cachedConnector.GetAllDataBaseReferences();
                    foreach (DataBaseRef dbRef in allDatabases)
                    {
                        await _cachedConnector.ClearDataCacheAsync(dbRef);
                    }
                    _logger.LogInformation("All data caches cleared successfully");
                    return Ok(new { message = "All data caches cleared successfully" });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error clearing all data caches");
                    return StatusCode(500, new { message = "Error clearing all data caches", error = ex.Message });
                }
            }
        }

        /// <summary>
        /// Clears hierarchy cache for all databases.
        /// </summary>
        /// <returns>A message indicating the result of the operation.</returns>
        /// <response code="200">All hierarchy caches were successfully cleared.</response>
        [HttpDelete("hierarchy")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(object), 200)]
        public ActionResult ClearAllHierarchyCaches()
        {
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                { LoggerConsts.CLASS_NAME, nameof(CacheController) },
                { LoggerConsts.METHOD_NAME, nameof(ClearAllHierarchyCaches) }
            }))
            {
                try
                {
                    IReadOnlyCollection<DataBaseRef> allDatabases = _cachedConnector.GetAllDataBaseReferences();
                    foreach (DataBaseRef dbRef in allDatabases)
                    {
                        _cachedConnector.ClearHierarchyCache(dbRef);
                    }
                    _logger.LogInformation("All hierarchy caches cleared successfully");
                    return Ok(new { message = "All hierarchy caches cleared successfully" });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error clearing all hierarchy caches");
                    return StatusCode(500, new { message = "Error clearing all hierarchy caches", error = ex.Message });
                }
            }
        }

        /// <summary>
        /// Clears all cache entries for all databases.
        /// </summary>
        /// <returns>A message indicating the result of the operation.</returns>
        /// <response code="200">All caches were successfully cleared.</response>
        [HttpDelete]
        [Produces("application/json")]
        [ProducesResponseType(typeof(object), 200)]
        public async Task<ActionResult> ClearAllCaches()
        {
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                { LoggerConsts.CLASS_NAME, nameof(CacheController) },
                { LoggerConsts.METHOD_NAME, nameof(ClearAllCaches) }
            }))
            {
                try
                {
                    await _cachedConnector.ClearAllCachesAsync();
                    _logger.LogInformation("All caches cleared successfully");
                    return Ok(new { message = "All caches cleared successfully" });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error clearing all caches");
                    return StatusCode(500, new { message = "Error clearing all caches", error = ex.Message });
                }
            }
        }

        /// <summary>
        /// Clears last updated timestamp cache for a specific database.
        /// </summary>
        /// <param name="database">Name of the database for which to clear last updated timestamp cache.</param>
        /// <returns>A message indicating the result of the operation.</returns>
        /// <response code="200">Last updated timestamp cache was successfully cleared.</response>
        /// <response code="404">If the database was not found.</response>
        [HttpDelete("{database}/lastupdated")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult> ClearLastUpdatedCache([FromRoute] string database)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                { LoggerConsts.CLASS_NAME, nameof(CacheController) },
                { LoggerConsts.METHOD_NAME, nameof(ClearLastUpdatedCache) },
                { LoggerConsts.DB_ID, database }
            }))
            {
                DataBaseRef? dbRef = _cachedConnector.GetDataBaseReference(database);
                if (dbRef == null)
                {
                    _logger.LogWarning("Database not found");
                    return NotFound(new { message = "Database not found" });
                }

                try
                {
                    await _cachedConnector.ClearLastUpdatedCacheAsync(dbRef.Value);
                    _logger.LogInformation("Last updated timestamp cache for database {DatabaseId} cleared successfully", dbRef.Value.Id);
                    return Ok(new { message = $"Last updated timestamp cache for database '{dbRef.Value.Id}' cleared successfully" });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error clearing last updated timestamp cache for database {DatabaseId}", dbRef.Value.Id);
                    return StatusCode(500, new { message = $"Error clearing last updated timestamp cache for database '{dbRef.Value.Id}'", error = ex.Message });
                }
            }
        }

        /// <summary>
        /// Clears last updated timestamp cache for all databases.
        /// </summary>
        /// <returns>A message indicating the result of the operation.</returns>
        /// <response code="200">All last updated timestamp caches were successfully cleared.</response>
        [HttpDelete("lastupdated")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(object), 200)]
        public async Task<ActionResult> ClearAllLastUpdatedCaches()
        {
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                { LoggerConsts.CLASS_NAME, nameof(CacheController) },
                { LoggerConsts.METHOD_NAME, nameof(ClearAllLastUpdatedCaches) }
            }))
            {
                try
                {
                    IReadOnlyCollection<DataBaseRef> allDatabases = _cachedConnector.GetAllDataBaseReferences();
                    foreach (DataBaseRef dbRef in allDatabases)
                    {
                        await _cachedConnector.ClearLastUpdatedCacheAsync(dbRef);
                    }
                    _logger.LogInformation("All last updated timestamp caches cleared successfully");
                    return Ok(new { message = "All last updated timestamp caches cleared successfully" });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error clearing all last updated timestamp caches");
                    return StatusCode(500, new { message = "Error clearing all last updated timestamp caches", error = ex.Message });
                }
            }
        }
    }
}