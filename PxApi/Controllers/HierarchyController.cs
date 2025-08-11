using Microsoft.AspNetCore.Mvc;
using PxApi.Caching;
using PxApi.DataSources;
using PxApi.Models;
using PxApi.Utilities;

namespace PxApi.Controllers
{
    /// <summary>
    /// Controller for managing database hierarchies.
    /// </summary>
    /// <param name="cachedConnector"><see cref="ICachedDataBaseConnector"/> instance for accessing data and metadata.</param>"/>
    /// <param name="logger">Logger for logging warnings and errors</param>
    [Route("hierarchy")]
    [ApiController]
    public class HierarchyController(ICachedDataBaseConnector cachedConnector, ILogger<HierarchyController> logger) : ControllerBase
    {
        /// <summary>
        /// Retrieves the hierarchy for a specific database.
        /// </summary>
        /// <param name="database">Name of the database for which to get the hierarchy</param>
        /// <returns>A dictionary representing the hierarchy of the specified database</returns>
        /// <response code="200">Returns the hierarchy of the specified database represented as a dictionary.</response>
        /// <response code="404">If the database or its hierarchy can not be found</response>
        [HttpGet("{database}")]
        [Produces("application/json")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public ActionResult<Dictionary<Groupings, List<PxFileRef>>> GetHierarchy([FromRoute] string database)
        {
            using (logger.BeginScope(new Dictionary<string, object>()
            {
                { LoggerConsts.CLASS_NAME, nameof(HierarchyController) },
                { LoggerConsts.METHOD_NAME, nameof(GetHierarchy) },
                { LoggerConsts.DB_ID, nameof(database) }
            }))
            {
                DataBaseRef? dbRef = cachedConnector.GetDataBaseReference(database);
                if (dbRef == null)
                {
                    logger.LogWarning("Database not found");
                    return NotFound("Database not found");
                }

                if (!cachedConnector.TryGetDataBaseHierarchy(dbRef.Value, out Dictionary<string, List<string>>? hierarchy))
                {
                    logger.LogWarning("Hierarchy not found for database {DatabaseId}", dbRef.Value.Id);
                    return NotFound("Hierarchy not found");
                }

                return Ok(hierarchy);
            }
        }

        /// <summary>
        /// Updates the hierarchy for the specified database.
        /// </summary>
        /// <param name="database">Name of the database to update hierarchy for</param>
        /// <param name="hierarchy">A dictionary representing the new hierarchy to set for the database</param>
        /// <result code="200">If the database hierarchy was updated successfully</result>
        /// <result code="404">If the database was not found</result>
        [HttpPost("{database}")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(404)]
        public ActionResult UpdateHierarchy(
            [FromRoute] string database,
            [FromBody] Dictionary<string, List<string>> hierarchy)
        {
            using (logger.BeginScope(new Dictionary<string, object>
            {
                { LoggerConsts.CLASS_NAME, nameof(HierarchyController) },
                { LoggerConsts.METHOD_NAME, nameof(UpdateHierarchy) },
                { LoggerConsts.DB_ID, database }
            }))
            {
                DataBaseRef? dbRef = cachedConnector.GetDataBaseReference(database);
                if (dbRef == null)
                {
                    logger.LogWarning("Database not found");
                    return NotFound("Database not found");
                }

                try
                {
                    cachedConnector.SetDataBaseHierarchy(dbRef.Value, hierarchy);
                    logger.LogDebug("Database hierarchy updated successfully");
                    return Ok(new { message = "Hierarchy updated successfully" });
                }
                catch (InvalidOperationException ex)
                {
                    logger.LogWarning(ex, "Invalid operation while updating hierarchy for database. It is possible that the hierarchy is not configured for the database.");
                    return BadRequest("Invalid operation while updating hierarchy. It is possible that the hierarchy is not configured for the database.");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error updating hierarchy for database");
                    return StatusCode(500, "Error updating hierarchy");
                }
            }
        }
    }
}
