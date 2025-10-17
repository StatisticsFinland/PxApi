using Microsoft.AspNetCore.Mvc;
using Px.Utils.Models.Metadata;
using PxApi.Caching;
using PxApi.ModelBuilders;
using PxApi.Models.JsonStat;
using PxApi.Models; // Added for DataBaseRef and PxFileRef

namespace PxApi.Controllers
{
    /// <summary>
    /// Provides metadata endpoints for PX tables.
    /// </summary>
    [Route("meta")]
    [ApiController]
    public class MetadataController(ICachedDataSource cachedConnector) : ControllerBase
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
        [HttpGet("{database}/{table}")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(JsonStat2), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<JsonStat2>> GetTableMetadataById(
            [FromRoute] string database,
            [FromRoute] string table,
            [FromQuery] string? lang)
        {
            try
            {
                DataBaseRef? dbRef = cachedConnector.GetDataBaseReference(database);
                if (dbRef is null) return NotFound("Database not found.");
                PxFileRef? fileRef = await cachedConnector.GetFileReferenceCachedAsync(table, dbRef.Value);
                if (fileRef is null) return NotFound("Table not found.");

                IReadOnlyMatrixMetadata meta = await cachedConnector.GetMetadataCachedAsync(fileRef.Value);

                string resolvedLang = lang ?? meta.DefaultLanguage;
                if (!meta.AvailableLanguages.Contains(resolvedLang))
                {
                    return BadRequest("The content is not available in the requested language.");
                }

                JsonStat2 jsonStat2 = JsonStat2Builder.BuildJsonStat2(meta, resolvedLang);
                return Ok(jsonStat2);
            }
            catch (FileNotFoundException)
            {
                return NotFound("Resource not found.");
            }
            catch (Exception)
            {
                return StatusCode(500, "Unexpected server error.");
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
        [HttpHead("{database}/{table}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> HeadMetadataAsync(string database, string table, string? lang = null)
        {
            DataBaseRef? dbRef = cachedConnector.GetDataBaseReference(database);
            if (dbRef is null) return NotFound();
            PxFileRef? fileRef = await cachedConnector.GetFileReferenceCachedAsync(table, dbRef.Value);
            if (fileRef is null) return NotFound();
            IReadOnlyMatrixMetadata meta = await cachedConnector.GetMetadataCachedAsync(fileRef.Value);
            string resolvedLang = lang ?? meta.DefaultLanguage;
            if (!meta.AvailableLanguages.Contains(resolvedLang)) return BadRequest();
            return Ok();
        }

        /// <summary>
        /// Returns allowed HTTP methods for the metadata resource.
        /// </summary>
        /// <param name="database">Identifier of the database containing the table.</param>
        /// <param name="table">Identifier of the table.</param>
        /// <response code="200">Returns allowed methods in the Allow header.</response>
        [HttpOptions("{database}/{table}")]
        [ProducesResponseType(200)]
        public IActionResult OptionsMetadata(string database, string table)
        {
            Response.Headers.Allow = "GET,HEAD,OPTIONS";
            return Ok();
        }
    }
}