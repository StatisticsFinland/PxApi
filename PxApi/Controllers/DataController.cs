using Microsoft.AspNetCore.Mvc;
using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Metadata;
using Px.Utils.Models.Metadata.ExtensionMethods;
using PxApi.Caching;
using PxApi.Models;
using PxApi.Models.JsonStat;
using PxApi.Models.QueryFilters;
using PxApi.Utilities;

namespace PxApi.Controllers
{
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
            DataBaseRef dataBase = DataBaseRef.Create(database);
            PxFileRef pxFile = PxFileRef.Create(table, dataBase);
            Dictionary<string, Filter> filters = QueryFilterUtils.ConvertUrlParametersToFilters(parameters);
            IReadOnlyMatrixMetadata meta = await dataSource.GetMetadataCachedAsync(pxFile);
            MatrixMap requestMap = MetaFiltering.ApplyToMatrixMeta(meta, filters);
            DoubleDataValue[] data = await dataSource.GetDataCachedAsync(pxFile, requestMap);

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
            DataBaseRef dataBase = DataBaseRef.Create(database);
            PxFileRef pxFile = PxFileRef.Create(table, dataBase);
            IReadOnlyMatrixMetadata meta = await dataSource.GetMetadataCachedAsync(pxFile);
            MatrixMap requestMap = MetaFiltering.ApplyToMatrixMeta(meta, query);
            DoubleDataValue[] data = await dataSource.GetDataCachedAsync(pxFile, requestMap);

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
            [FromQuery] Dictionary<string, string> parameters
            )
        {
            // Convert URL parameters to Filter objects
            Dictionary<string, Filter> filters = QueryFilterUtils.ConvertUrlParametersToFilters(parameters);
            
            // Now process the request with filters
            return Ok();
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
            [FromBody] Dictionary<string, Filter> query
            )
        {
            return Ok();
        }
    }
}
