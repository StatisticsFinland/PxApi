using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Metadata.ExtensionMethods;
using Px.Utils.Models.Metadata;
using PxApi.Caching;
using PxApi.Configuration;
using PxApi.ModelBuilders;
using PxApi.Models.JsonStat;
using PxApi.Models.QueryFilters;
using PxApi.Models;
using PxApi.Utilities;
using Px.Utils.Models;
using PxApi.OpenApi;

namespace PxApi.Controllers
{
    /// <summary>
    /// Provides endpoints for retrieving and querying data in JSON-stat 2.0 or CSV formats via filter specifications supporting code, range and positional selection semantics.
    /// </summary>
    /// <param name="dataSource">Cached data source for accessing PX file metadata and values.</param>
    /// <param name="logger">Logger instance.</param>
    [ApiController]
    [Route("data")]
    public class DataController(ICachedDataSource dataSource, ILogger<DataController> logger) : ControllerBase
    {
        private static readonly string[] SupportedMediaTypes = ["application/json", "text/csv"];

        /// <summary>
        /// Retrieves data using query string filters. Content negotiation based on the Accept header (application/json for JSON-stat, text/csv for CSV; */* treated as JSON).
        /// </summary>
        /// <param name="database">Database identifier containing the table.</param>
        /// <param name="table">PX table identifier.</param>
        /// <param name="filters">Array of filter specifications 'dimension:filterType=value'. Supported filterType: code, from, to, first, last. One filter per dimension; first/last require positive integers; '*' wildcard matches zero or more characters in code/from/to values; multiple code values separated by commas.</param>
        /// <param name="lang">Optional language code; defaults to table's default language when omitted.</param>
        /// <returns>Data in JSON-stat or CSV format depending on Accept header.</returns>
        /// <response code="200">Successful query returning data.</response>
        /// <response code="400">Invalid filters, duplicate dimensions, or invalid language.</response>
        /// <response code="404">Database or table not found.</response>
        /// <response code="406">Requested media type not supported by endpoint.</response>
        /// <response code="413">Request exceeds maximum allowed cell count.</response>
        /// <response code="415">Unsupported Content-Type header.</response>
        [HttpGet("{database}/{table}")]
        [OperationId("getData")]
        [Produces("application/json", "text/csv")]
        [ProducesResponseType(typeof(JsonStat2), 200, "application/json")]
        [ProducesResponseType(typeof(string), 200, "text/csv")]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(406)]
        [ProducesResponseType(413)]
        [ProducesResponseType(415)]
        public async Task<IActionResult> GetDataAsync(
            [FromRoute] string database,
            [FromRoute] string table,
            [FromQuery] string[]? filters = null,
            [FromQuery] string? lang = null)
        {
            using (logger.BeginScope(new Dictionary<string, object>()
            {
                { LoggerConsts.CLASS_NAME, nameof(DataController) },
                { LoggerConsts.METHOD_NAME, nameof(GetDataAsync) },
                { LoggerConsts.DB_ID, database },
                { LoggerConsts.PX_FILE, table }
            }))
            {
                Dictionary<string, Filter> query = QueryFilterUtils.ConvertFiltersArrayToFilters(filters ?? []);
                return await GenerateResponse(database, table, lang, query);
            }
        }

        /// <summary>
        /// Retrieves data using a JSON body of filter objects. Content negotiation identical to GET. Body maps dimension codes to filter definitions.
        /// </summary>
        /// <param name="database">Database identifier containing the table.</param>
        /// <param name="table">PX table identifier.</param>
        /// <param name="query">Dictionary of filters keyed by dimension code. Each value defines type and associated query data.</param>
        /// <param name="lang">Optional language code; defaults to table's default language when omitted.</param>
        /// <returns>Data in JSON-stat or CSV format depending on Accept header.</returns>
        /// <response code="200">Successful query returning data.</response>
        /// <response code="400">Invalid filter body or invalid language.</response>
        /// <response code="404">Database or table not found.</response>
        /// <response code="406">Requested media type not supported by endpoint.</response>
        /// <response code="413">Request exceeds maximum allowed cell count.</response>
        /// <response code="415">Unsupported Content-Type for request body.</response>
        [HttpPost("{database}/{table}")]
        [OperationId("postData")]
        [Consumes("application/json")]
        [Produces("application/json", "text/csv")]
        [ProducesResponseType(typeof(JsonStat2), 200, "application/json")]
        [ProducesResponseType(typeof(string), 200, "text/csv")]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(406)]
        [ProducesResponseType(413)]
        [ProducesResponseType(415)]
        public async Task<ActionResult> PostDataAsync(
            [FromRoute] string database,
            [FromRoute] string table,
            [FromBody] Dictionary<string, Filter> query,
            [FromQuery] string? lang = null)
        {
            using (logger.BeginScope(new Dictionary<string, object>()
            {
                { LoggerConsts.CLASS_NAME, nameof(DataController) },
                { LoggerConsts.METHOD_NAME, nameof(PostDataAsync) },
                { LoggerConsts.DB_ID, database },
                { LoggerConsts.PX_FILE, table }
            }))
            {
                return await GenerateResponse(database, table, lang, query);
            }
        }

        /// <summary>
        /// Returns allowed HTTP methods for the data resource. Useful for CORS pre-flight or client capability discovery.
        /// </summary>
        /// <param name="database">Database identifier containing the table.</param>
        /// <param name="table">PX table identifier.</param>
        /// <response code="200">Returns allowed methods in the Allow response header.</response>
        [HttpOptions("{database}/{table}")]
        [OperationId("optionsData")]
        [ProducesResponseType(200)]
        public IActionResult OptionsData(string database, string table)
        {
            Response.Headers.Allow = "GET,POST,HEAD,OPTIONS";
            return Ok();
        }

        /// <summary>
        /// HEAD endpoint returning only headers (no body) for the data query target. Validates existence and language availability.
        /// </summary>
        /// <param name="database">Database identifier containing the table.</param>
        /// <param name="table">PX table identifier.</param>
        /// <param name="lang">Optional language code.</param>
        /// <response code="200">Resource exists.</response>
        /// <response code="400">Invalid language requested.</response>
        /// <response code="404">Database or table not found.</response>
        [HttpHead("{database}/{table}")]
        [OperationId("headData")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> HeadDataAsync(string database, string table, string? lang = null)
        {
            DataBaseRef? dbRef = dataSource.GetDataBaseReference(database);
            if (dbRef is null) return NotFound();
            PxFileRef? fileRef = await dataSource.GetFileReferenceCachedAsync(table, dbRef.Value);
            if (fileRef is null) return NotFound();
            IReadOnlyMatrixMetadata meta = await dataSource.GetMetadataCachedAsync(fileRef.Value);
            string actualLang = lang ?? meta.DefaultLanguage;
            if (!meta.AvailableLanguages.Contains(actualLang)) return BadRequest();
            return Ok();
        }

        private async Task<ActionResult> GenerateResponse(string database, string table, string? lang, Dictionary<string, Filter> query)
        {
            DataBaseRef? dbRef = dataSource.GetDataBaseReference(database);
            if (dbRef is null)
            {
                const string message = "The requested database was not found.";
                logger.LogDebug(message);
                return NotFound(message);
            }
            PxFileRef? fileRef = await dataSource.GetFileReferenceCachedAsync(table, dbRef.Value);
            if (fileRef is null)
            {
                const string message = "The requested Px table was not found.";
                logger.LogDebug(message);
                return NotFound(message);
            }

            try
            {
                IReadOnlyMatrixMetadata meta = await dataSource.GetMetadataCachedAsync(fileRef.Value);

                string actualLang = lang ?? meta.DefaultLanguage;
                if (!meta.AvailableLanguages.Contains(actualLang))
                {
                    logger.LogDebug("The Requested language was not available in the table {Table}.", fileRef.Value.Id);
                    return BadRequest("The content is not available in the requested language.");
                }

                MatrixMap requestMap = MetaFiltering.ApplyToMatrixMeta(meta, query);

                long maxSize = AppSettings.Active.QueryLimits.JsonStatMaxCells;
                int size = requestMap.GetSize();
                if (size > maxSize)
                {
                    logger.LogInformation("Too large request received. Size: {Size}.", size);
                    return StatusCode(413, $"The request is too large. Please narrow down the query. Maximum size is {maxSize} cells.");
                }

                DoubleDataValue[] data = await dataSource.GetDataCachedAsync(fileRef.Value, requestMap);

                // Use proper content negotiation with quality values
                IList<MediaTypeHeaderValue> acceptHeaderValues = Request.GetTypedHeaders().Accept;
                string? bestMatch = ContentNegotiation.GetBestMatch(acceptHeaderValues, SupportedMediaTypes);

                if (bestMatch == "text/csv")
                {
                    Matrix<DoubleDataValue> requestMatrix = new(meta.GetTransform(requestMap), data);
                    return Content(CsvBuilder.BuildCsvResponse(requestMatrix, actualLang, meta), "text/csv");
                }
                if (bestMatch == "application/json")
                {
                    IReadOnlyList<TableGroup> groupings = await dataSource.GetGroupingsCachedAsync(fileRef.Value);
                    JsonStat2 jsonStat = JsonStat2Builder.BuildJsonStat2(meta.GetTransform(requestMap), groupings, data, actualLang);
                    return Ok(jsonStat);
                }
            }
            catch (ArgumentException argEx)
            {
                logger.LogDebug(argEx, "Argument exception occurred while processing request: {Message}", argEx.Message);
                return BadRequest(argEx.Message);
            }

            return StatusCode(406); // Not Acceptable for unsupported Accept header values.
        }
    }
}
