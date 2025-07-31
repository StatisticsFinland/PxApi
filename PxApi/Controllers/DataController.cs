using Microsoft.AspNetCore.Mvc;
using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Metadata;
using Px.Utils.Models.Metadata.ExtensionMethods;
using PxApi.Caching;
using PxApi.Models;
using PxApi.ModelBuilders;
using PxApi.Models.JsonStat;
using PxApi.Models.QueryFilters;
using PxApi.Utilities;

namespace PxApi.Controllers
{
    /// <summary>
    /// Provides endpoints for retrieving and querying data in various formats, such as JSON and JSON-stat.
    /// </summary>
    /// <param name="dataSource"></param>
    [ApiController]
    [Route("data")]
    public class DataController(ICachedDataBaseConnector dataSource) : ControllerBase
    {
        [HttpGet]
        [Route("json/{database}/{table}")]
        [Produces("application/json")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<DataResponse>> GetJsonAsync(
            [FromRoute] string database,
            [FromRoute] string table,
            [FromQuery] Dictionary<string, string> parameters
            )
        {
            DataBaseRef? dbRef = dataSource.GetDataBaseReference(database);
            if (dbRef is null) return NotFound();
            PxFileRef? fileRef = await dataSource.GetFileReferenceCachedAsync(table, dbRef.Value);
            if (fileRef is null) return NotFound();

            Dictionary<string, Filter> filters = QueryFilterUtils.ConvertUrlParametersToFilters(parameters);
            IReadOnlyMatrixMetadata meta = await dataSource.GetMetadataCachedAsync(fileRef.Value);
            MatrixMap requestMap = MetaFiltering.ApplyToMatrixMeta(meta, filters);
            DoubleDataValue[] data = await dataSource.GetDataCachedAsync(fileRef.Value, requestMap);

            return Ok(new DataResponse
            {
                LastUpdated = meta.GetContentDimension().Values.Map(v => v.LastUpdated).Max(),
                MetaCodes = requestMap,
                Data = data
            });
        }

        [HttpPost]
        [Route("json/{database}/{table}")]
        [Produces("application/json")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<DataResponse>> PostJsonAsync(
            [FromRoute] string database,
            [FromRoute] string table,
            [FromBody] Dictionary<string, Filter> query
            )
        {
            DataBaseRef? dbRef = dataSource.GetDataBaseReference(database);
            if (dbRef is null) return NotFound();
            PxFileRef? fileRef = await dataSource.GetFileReferenceCachedAsync(table, dbRef.Value);
            if (fileRef is null) return NotFound();

            IReadOnlyMatrixMetadata meta = await dataSource.GetMetadataCachedAsync(fileRef.Value);
            MatrixMap requestMap = MetaFiltering.ApplyToMatrixMeta(meta, query);
            DoubleDataValue[] data = await dataSource.GetDataCachedAsync(fileRef.Value, requestMap);

            return Ok(new DataResponse
            {
                LastUpdated = meta.GetContentDimension().Values.Map(v => v.LastUpdated).Max(),
                MetaCodes = requestMap,
                Data = data
            });
        }

        [HttpGet]
        [Route("{database}/{table}/json-stat")]
        [Produces("application/json")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<JsonStat2>> GetJsonStatAsync(
            [FromRoute] string database,
            [FromRoute] string table,
            [FromQuery] Dictionary<string, string> parameters,
            [FromQuery] string? lang = null
            )
        {
            try
            {
                DataBaseRef? dbRef = dataSource.GetDataBaseReference(database);
                if (dbRef is null) return NotFound();
                PxFileRef? fileRef = await dataSource.GetFileReferenceCachedAsync(table, dbRef.Value);
                if (fileRef is null) return NotFound();

                Dictionary<string, Filter> filters = QueryFilterUtils.ConvertUrlParametersToFilters(parameters);
                IReadOnlyMatrixMetadata meta = await dataSource.GetMetadataCachedAsync(fileRef.Value);

                string actualLang = lang ?? meta.DefaultLanguage;
                if (!meta.AvailableLanguages.Contains(actualLang))
                {
                    return BadRequest("The content is not available in the requested language.");
                }

                MatrixMap requestMap = MetaFiltering.ApplyToMatrixMeta(meta, filters);
                DoubleDataValue[] data = await dataSource.GetDataCachedAsync(fileRef.Value, requestMap);
                JsonStat2 jsonStat = ModelBuilder.BuildJsonStat2(meta, data, actualLang);

                return Ok(jsonStat);
            }
            catch (FileNotFoundException)
            {
                return NotFound("Table or database not found");
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        [Route("{database}/{table}/json-stat")]
        [Produces("application/json")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<JsonStat2>> PostJsonStatAsync(
            [FromRoute] string database,
            [FromRoute] string table,
            [FromBody] Dictionary<string, Filter> query,
            [FromQuery] string? lang = null
            )
        {
            try
            {
                DataBaseRef? dbRef = dataSource.GetDataBaseReference(database);
                if (dbRef is null) return NotFound();
                PxFileRef? fileRef = await dataSource.GetFileReferenceCachedAsync(table, dbRef.Value);
                if (fileRef is null) return NotFound();

                IReadOnlyMatrixMetadata meta = await dataSource.GetMetadataCachedAsync(fileRef.Value);
                
                // Validate language
                string actualLang = lang ?? meta.DefaultLanguage;
                if (!meta.AvailableLanguages.Contains(actualLang))
                {
                    return BadRequest("The content is not available in the requested language.");
                }
                
                MatrixMap requestMap = MetaFiltering.ApplyToMatrixMeta(meta, query);
                DoubleDataValue[] data = await dataSource.GetDataCachedAsync(fileRef.Value, requestMap);
                JsonStat2 jsonStat = ModelBuilder.BuildJsonStat2(meta, data, actualLang);
                
                return Ok(jsonStat);
            }
            catch (FileNotFoundException)
            {
                return NotFound("Table or database not found");
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
