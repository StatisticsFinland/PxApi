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
        /// <param name="ct">Cancellation token bound to the client request lifetime.</param>
        /// <returns>JSON-stat 2.0 metadata object for the specified table.</returns>
        /// <response code="200">Metadata returned successfully.</response>
        /// <response code="400">Requested language not available.</response>
        /// <response code="404">Database or table not found.</response>
        [HttpGet("{database}/tables/{table}")]
        [OperationId("getTableMeta")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(JsonStat2), 200)]
        [ProducesResponseType(typeof(string), 400)]
        [ProducesResponseType(typeof(string), 404)]
        public async Task<ActionResult<JsonStat2>> GetTableMetadataById(
            [FromRoute] string database,
            [FromRoute] string table,
            [FromQuery] string? lang,
            CancellationToken ct = default)
        {
            try
            {
                DataBaseRef? dbRef = cachedConnector.GetDataBaseReference(database);
                if (dbRef is null)
                {
                    using (logger.BeginResourceNotFoundScope())
                    {
                        auditLogService.LogAuditEvent();
                        return NotFound("Database not found.");
                    }
                }

                PxFileRef? fileRef = await cachedConnector.GetFileReferenceCachedAsync(table, dbRef.Value, ct);
                if (fileRef is null)
                {
                    using (logger.BeginResourceNotFoundScope(dbRef.Value.Id))
                    {
                        auditLogService.LogAuditEvent();
                        return NotFound("Table not found.");
                    }
                }

                using (logger.BeginResourceScope(dbRef.Value.Id, fileRef.Value.Id))
                {
                    auditLogService.LogAuditEvent();
                    IReadOnlyMatrixMetadata meta = await cachedConnector.GetMetadataCachedAsync(fileRef.Value, ct);

                    string resolvedLang = lang ?? meta.DefaultLanguage;
                    if (!meta.AvailableLanguages.Contains(resolvedLang))
                    {
                        return BadRequest("The content is not available in the requested language.");
                    }

                    JsonStat2 jsonStat2 = JsonStat2Builder.BuildJsonStat2(meta, resolvedLang);
                    return Ok(jsonStat2);
                }
            }
            catch (FileNotFoundException)
            {
                return NotFound("Resource not found.");
            }
        }

        /// <summary>
        /// HEAD endpoint returning only headers for the metadata resource.
        /// </summary>
        /// <param name="database">Identifier of the database containing the table.</param>
        /// <param name="table">Identifier of the table.</param>
        /// <param name="lang">Optional language code.</param>
        /// <param name="ct">Cancellation token bound to the client request lifetime.</param>
        /// <response code="200">Resource exists.</response>
        /// <response code="400">Requested language not available.</response>
        /// <response code="404">Database or table not found.</response>
        [HttpHead("{database}/tables/{table}")]
        [OperationId("headTableMeta")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> HeadMetadataAsync(string database, string table, string? lang = null, CancellationToken ct = default)
        {
            DataBaseRef? dbRef = cachedConnector.GetDataBaseReference(database);
            if (dbRef is null)
            {
                using (logger.BeginResourceNotFoundScope())
                {
                    auditLogService.LogAuditEvent();
                    return NotFound();
                }
            }

            PxFileRef? fileRef = await cachedConnector.GetFileReferenceCachedAsync(table, dbRef.Value, ct);
            if (fileRef is null)
            {
                using (logger.BeginResourceNotFoundScope(dbRef.Value.Id))
                {
                    auditLogService.LogAuditEvent();
                    return NotFound();
                }
            }

            using (logger.BeginResourceScope(dbRef.Value.Id, fileRef.Value.Id))
            {
                auditLogService.LogAuditEvent();
                IReadOnlyMatrixMetadata meta = await cachedConnector.GetMetadataCachedAsync(fileRef.Value, ct);
                string resolvedLang = lang ?? meta.DefaultLanguage;
                if (!meta.AvailableLanguages.Contains(resolvedLang)) return BadRequest();
                return Ok();
            }
        }

        /// <summary>
        /// Returns allowed HTTP methods for the metadata resource.
        /// </summary>
        /// <param name="database">Identifier of the database containing the table.</param>
        /// <param name="table">Identifier of the table.</param>
        /// <response code="200">Returns allowed methods in the Allow header.</response>
        [HttpOptions("{database}/tables/{table}")]
        [OperationId("optionsTableMeta")]
        [ProducesResponseType(200)]
        public IActionResult OptionsMetadata(string database, string table)
        {
            Response.Headers.Allow = "GET,HEAD,OPTIONS";
            auditLogService.LogAuditEvent();
            return Ok();
        }
    }
}
