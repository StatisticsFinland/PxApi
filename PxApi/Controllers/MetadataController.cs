using Microsoft.AspNetCore.Mvc;
using Px.Utils.Models.Metadata;
using PxApi.Caching;
using PxApi.ModelBuilders;
using PxApi.Models.JsonStat;
using PxApi.Models;
using PxApi.OpenApi;
using PxApi.Services;
using PxApi.Utilities;
using PxApi.Authentication;

namespace PxApi.Controllers
{
    /// <summary>
    /// Provides metadata endpoints for PX tables.
    /// </summary>
    [ApiKeyAuth]
    [Route("meta/databases")]
    [ApiController]
    public class MetadataController(ICachedDataSource cachedConnector, ILogger<MetadataController> logger, IAuditLogService auditLogService) : ControllerBase
    {
        /// <summary>
        /// Gets metadata for a single table in JSON-stat 2.0 format (no data values filtering applied).
        /// </summary>
        /// <param name="database">Identifier of the database containing the table.</param>
        /// <param name="table">Identifier of the table.</param>
        /// <param name="lang">Optional language code; if omitted the table's default language is used.</param>
        /// <returns>JSON-stat 2.0 metadata object for the specified table.</returns>
        /// <response code="200">Metadata returned successfully.</response>
        /// <response code="400">Requested language not available.</response>
        /// <response code="404">Database or table not found.</response>
        /// <response code="500">Unexpected server error.</response>
        [HttpGet("{database}/tables/{table}")]
        [OperationId("getTableMeta")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(JsonStat2), 200)]
        [ProducesResponseType(typeof(string), 400)]
        [ProducesResponseType(typeof(string), 404)]
        [ProducesResponseType(typeof(string), 500)]
        public async Task<ActionResult<JsonStat2>> GetTableMetadataById(
            [FromRoute] string database,
            [FromRoute] string table,
            [FromQuery] string? lang)
        {
            using (logger.BeginScope(new Dictionary<string, object>
                {
                    { LoggerConsts.CONTROLLER, nameof(MetadataController) },
                    { LoggerConsts.ACTION, nameof(GetTableMetadataById) }
                }))
            {
                try
                {
                    DataBaseRef? dbRef = cachedConnector.GetDataBaseReference(database);
                    if (dbRef is null)
                    {
                        using (logger.BeginScope(new Dictionary<string, object>
                        {
                            { LoggerConsts.DB_ID, LoggerConsts.NOT_FOUND_PLACEHOLDER },
                            { LoggerConsts.PX_FILE, LoggerConsts.NOT_FOUND_PLACEHOLDER }
                        }))
                        {
                            auditLogService.LogAuditEvent();
                            return NotFound("Database not found.");
                        }
                    }

                    PxFileRef? fileRef = await cachedConnector.GetFileReferenceCachedAsync(table, dbRef.Value);
                    if (fileRef is null)
                    {
                        using (logger.BeginScope(new Dictionary<string, object>
                        {
                            { LoggerConsts.DB_ID, dbRef.Value.Id },
                            { LoggerConsts.PX_FILE, LoggerConsts.NOT_FOUND_PLACEHOLDER }
                        }))
                        {
                            auditLogService.LogAuditEvent();
                            return NotFound("Table not found.");
                        }
                    }

                    using (logger.BeginScope(new Dictionary<string, object>
                        {
                            { LoggerConsts.DB_ID, dbRef.Value.Id },
                            { LoggerConsts.PX_FILE, fileRef.Value.Id }
                        }))
                    {
                        auditLogService.LogAuditEvent();
                        IReadOnlyMatrixMetadata meta = await cachedConnector.GetMetadataCachedAsync(fileRef.Value);

                        string resolvedLang = lang ?? meta.DefaultLanguage;
                        if (!meta.AvailableLanguages.Contains(resolvedLang))
                        {
                            return BadRequest("The content is not available in the requested language.");
                        }

                        IReadOnlyList<TableGroup> groupings = await cachedConnector.GetGroupingsCachedAsync(fileRef.Value);
                        JsonStat2 jsonStat2 = JsonStat2Builder.BuildJsonStat2(meta, groupings, resolvedLang);
                        return Ok(jsonStat2);
                    }
                }
                catch (FileNotFoundException)
                {
                    return NotFound("Resource not found.");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An unexpected error occurred while processing the request.");
                    return StatusCode(500, "Unexpected server error.");
                }
            }
        }

        /// <summary>
        /// HEAD endpoint returning only headers for the metadata resource.
        /// </summary>
        /// <param name="database">Identifier of the database containing the table.</param>
        /// <param name="table">Identifier of the table.</param>
        /// <param name="lang">Optional language code.</param>
        /// <response code="200">Resource exists.</response>
        /// <response code="400">Requested language not available.</response>
        /// <response code="404">Database or table not found.</response>
        /// <response code="500">Unexpected server error.</response>
        [HttpHead("{database}/tables/{table}")]
        [OperationId("headTableMeta")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> HeadMetadataAsync(string database, string table, string? lang = null)
        {
            using (logger.BeginScope(new Dictionary<string, object>
                {
                    { LoggerConsts.CONTROLLER, nameof(MetadataController) },
                    { LoggerConsts.ACTION, nameof(HeadMetadataAsync) }
                }))
            {
                try
                {
                    DataBaseRef? dbRef = cachedConnector.GetDataBaseReference(database);
                    if (dbRef is null)
                    {
                        using (logger.BeginScope(new Dictionary<string, object>
                        {
                            { LoggerConsts.DB_ID, LoggerConsts.NOT_FOUND_PLACEHOLDER },
                            { LoggerConsts.PX_FILE, LoggerConsts.NOT_FOUND_PLACEHOLDER }
                        }))
                        {
                            auditLogService.LogAuditEvent();
                            return NotFound();
                        }
                    }

                    PxFileRef? fileRef = await cachedConnector.GetFileReferenceCachedAsync(table, dbRef.Value);
                    if (fileRef is null)
                    {
                        using (logger.BeginScope(new Dictionary<string, object>
                        {
                            { LoggerConsts.DB_ID, dbRef.Value.Id },
                            { LoggerConsts.PX_FILE, LoggerConsts.NOT_FOUND_PLACEHOLDER }
                        }))
                        {
                            auditLogService.LogAuditEvent();
                            return NotFound();
                        }
                    }

                    using (logger.BeginScope(new Dictionary<string, object>
                        {
                            { LoggerConsts.DB_ID, dbRef.Value.Id },
                            { LoggerConsts.PX_FILE, fileRef.Value.Id }
                        }))
                    {
                        auditLogService.LogAuditEvent();
                        IReadOnlyMatrixMetadata meta = await cachedConnector.GetMetadataCachedAsync(fileRef.Value);
                        string resolvedLang = lang ?? meta.DefaultLanguage;
                        if (!meta.AvailableLanguages.Contains(resolvedLang)) return BadRequest();
                        return Ok();
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An unexpected error occurred while processing the request.");
                    return StatusCode(500);
                }
            }
        }

        /// <summary>
        /// Returns allowed HTTP methods for the metadata resource.
        /// </summary>
        /// <param name="database">Identifier of the database containing the table.</param>
        /// <param name="table">Identifier of the table.</param>
        /// <response code="200">Returns allowed methods in the Allow header.</response>
        /// <response code="500">Unexpected server error.</response>
        [HttpOptions("{database}/tables/{table}")]
        [OperationId("optionsTableMeta")]
        [ProducesResponseType(200)]
        [ProducesResponseType(500)]
        public IActionResult OptionsMetadata(string database, string table)
        {
            using (logger.BeginScope(new Dictionary<string, object>
            {
                { LoggerConsts.CONTROLLER, nameof(MetadataController) },
                { LoggerConsts.ACTION, nameof(OptionsMetadata) }
            }))
            {
                Response.Headers.Allow = "GET,HEAD,OPTIONS";
                auditLogService.LogAuditEvent();
                return Ok();
            }
        }
    }
}