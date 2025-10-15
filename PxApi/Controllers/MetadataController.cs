using Microsoft.AspNetCore.Mvc;
using Px.Utils.Models.Metadata;
using PxApi.Caching;
using PxApi.ModelBuilders;
using PxApi.Models;
using PxApi.Models.JsonStat;

namespace PxApi.Controllers
{
    /// <summary>
    /// Controller for /meta endpoint.
    /// Contains methods for getting metadata about tables and dimensions.
    /// </summary>
    [Route("meta")]
    [ApiController]
    public class MetadataController(ICachedDataSource cachedConnector) : ControllerBase
    {
        /// <summary>
        /// Get metadata for a single table in json-stat 2.0 format.
        /// </summary>
        /// <param name="database">Name of the database that contains the table</param>
        /// <param name="table">Name of the table</param>
        /// <param name="lang">
        /// [Optional] Language used to get the metadata.
        /// If left empty uses the default language of the table.
        /// The provided language must be available in the table.
        /// </param>
        /// <returns><see cref="JsonStat2"/> object containing metadata of the <paramref name="table"/>.</returns> 
        /// <response code="200">Returns the table metadata</response>
        /// <response code="400">If the metadata is not available in the specified language.</response>
        /// <response code="404">If the table or database is not found.</response>
        [HttpGet("{database}/{table}")]
        [Produces("application/json")] 
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<JsonStat2>> GetTableMetadataById(
            [FromRoute] string database,
            [FromRoute] string table,
            [FromQuery] string? lang)
        {
            try
            {
                DataBaseRef? dbRef = cachedConnector.GetDataBaseReference(database);
                if(dbRef is null) return NotFound();
                PxFileRef? fileRef = await cachedConnector.GetFileReferenceCachedAsync(table, dbRef.Value);
                if (fileRef is null) return NotFound();

                IReadOnlyMatrixMetadata meta = await cachedConnector.GetMetadataCachedAsync(fileRef.Value);

                if (lang is null || meta.AvailableLanguages.Contains(lang))
                {
                    JsonStat2 jsonStat2 = JsonStat2Builder.BuildJsonStat2(meta, lang);
                    return Ok(jsonStat2);
                }
                else
                {
                    return BadRequest("The content is not available in the requested language.");
                }
            }
            catch (FileNotFoundException)
            {
                return NotFound();
            }
        }
    }
}