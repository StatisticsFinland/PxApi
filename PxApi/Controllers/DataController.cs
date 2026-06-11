using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Metadata.ExtensionMethods;
using Px.Utils.Models.Metadata;
using Px.Utils.Models;
using PxApi.Caching;
using PxApi.Configuration;
using PxApi.ModelBuilders;
using PxApi.Models.JsonStat;
using PxApi.Models.QueryFilters;
using PxApi.Models;
using PxApi.OpenApi;
using PxApi.Services;
using PxApi.Utilities;
using PxApi.Authentication;
using PxApi.Exceptions;

namespace PxApi.Controllers
{
    /// <summary>
    /// Provides endpoints for retrieving and querying data in JSON-stat 2.0 or CSV formats via filter specifications supporting code, range and positional selection semantics.
    /// </summary>
    /// <param name="dataSource">Cached data source for accessing PX file metadata and values.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="auditLogService">Audit logging service.</param>
    [ApiKeyAuth]
    [ApiController]
    [Route("data/databases")]
    public class DataController(ICachedDataSource dataSource, ILogger<DataController> logger, IAuditLogService auditLogService) : ControllerBase
    {
        private const string APPLICATION_JSON = "application/json";
        private const string TEXT_CSV = "text/csv";

        private static readonly string[] SupportedMediaTypes = [APPLICATION_JSON, TEXT_CSV];

        /// <summary>
        /// Retrieves data using query string filters. Content negotiation based on the Accept header (application/json for JSON-stat, text/csv for CSV; */* treated as JSON).
        /// </summary>
        /// <param name="database">Database identifier containing the table.</param>
        /// <param name="table">PX table identifier.</param>
        /// <param name="filters">Array of filter specifications 'dimension:filterType=value'. Supported filterType: code, from, to, first, last. One filter per dimension; first/last require positive integers; '*' wildcard matches zero or more characters in code/from/to values; multiple code values separated by commas.</param>
        /// <param name="lang">Optional language code; defaults to table's default language when omitted.</param>
        /// <param name="ct">Cancellation token bound to the client request lifetime.</param>
        /// <returns>Data in JSON-stat or CSV format depending on Accept header.</returns>
        /// <response code="200">Successful query returning data.</response>
        /// <response code="400">Invalid filters, duplicate dimensions, or invalid language.</response>
        /// <response code="404">Database or table not found.</response>
        /// <response code="406">Requested media type not supported by endpoint.</response>
        /// <response code="413">Request exceeds maximum allowed cell count.</response>
        /// <response code="415">Unsupported Content-Type header.</response>
        /// <response code="503">The request is valid but the data is temporarily unavailable due to a database update.</response>
        [HttpGet("{database}/tables/{table}")]
        [OperationId("getData")]
        [Produces(APPLICATION_JSON, TEXT_CSV)]
        [ProducesResponseType(typeof(JsonStat2), 200, APPLICATION_JSON)]
        [ProducesResponseType(typeof(string), 200, TEXT_CSV)]
        [ProducesResponseType(typeof(string), 400)]
        [ProducesResponseType(typeof(string), 404)]
        [ProducesResponseType(typeof(string), 406)]
        [ProducesResponseType(typeof(string), 413)]
        [ProducesResponseType(typeof(string), 415)]
        [ProducesResponseType(typeof(string), 503)]
        public async Task<IActionResult> GetDataAsync(
            [FromRoute] string database,
            [FromRoute] string table,
            [FromQuery] string[]? filters = null,
            [FromQuery] string? lang = null,
            CancellationToken ct = default)
        {
            Dictionary<string, Filter> query;
            try
            {
                query = QueryFilterUtils.ConvertFiltersArrayToFilters(filters ?? []);
            }
            catch (ArgumentException argEx)
            {
                logger.LogDebug(argEx, "Invalid filters provided: {Message}", argEx.Message);
                return BadRequest(HttpConsts.BAD_REQUEST_PARAMS);
            }

            return await GenerateResponse(database, table, lang, query, ct);
        }

        /// <summary>
        /// Retrieves data using a JSON body of filter objects.
        /// </summary>
        /// <param name="database">Database identifier containing the table.</param>
        /// <param name="table">PX table identifier.</param>
        /// <param name="query">Dictionary of filters keyed by dimension code. Each value defines type and associated query data.</param>
        /// <param name="lang">Optional language code; defaults to table's default language when omitted.</param>
        /// <param name="ct">Cancellation token bound to the client request lifetime.</param>
        /// <returns>Data in JSON-stat or CSV format depending on Accept header.</returns>
        /// <response code="200">Successful query returning data.</response>
        /// <response code="400">Invalid filter body or invalid language.</response>
        /// <response code="404">Database or table not found.</response>
        /// <response code="406">Requested media type not supported by endpoint.</response>
        /// <response code="413">Request exceeds maximum allowed cell count.</response>
        /// <response code="415">Unsupported Content-Type for request body.</response>
        /// <response code="503">The request is valid but the data is temporarily unavailable due to a database update.</response>
        [HttpPost("{database}/tables/{table}")]
        [OperationId("postData")]
        [Consumes(APPLICATION_JSON)]
        [Produces(APPLICATION_JSON, TEXT_CSV)]
        [ProducesResponseType(typeof(JsonStat2), 200, APPLICATION_JSON)]
        [ProducesResponseType(typeof(string), 200, TEXT_CSV)]
        [ProducesResponseType(typeof(string), 400)]
        [ProducesResponseType(typeof(string), 404)]
        [ProducesResponseType(typeof(string), 406)]
        [ProducesResponseType(typeof(string), 413)]
        [ProducesResponseType(typeof(string), 415)]
        [ProducesResponseType(typeof(string), 503)]
        public async Task<ActionResult> PostDataAsync(
            [FromRoute] string database,
            [FromRoute] string table,
            [FromBody] Dictionary<string, Filter> query,
            [FromQuery] string? lang = null,
            CancellationToken ct = default)
        {
            return await GenerateResponse(database, table, lang, query, ct);
        }

        /// <summary>
        /// Returns allowed HTTP methods for the data resource.
        /// </summary>
        /// <param name="database">Database identifier containing the table.</param>
        /// <param name="table">PX table identifier.</param>
        /// <response code="200">Returns allowed methods in the Allow response header.</response>
        [HttpOptions("{database}/tables/{table}")]
        [OperationId("optionsData")]
        [ProducesResponseType(200)]
        public IActionResult OptionsData(string database, string table)
        {
            Response.Headers.Allow = "GET,POST,HEAD,OPTIONS";
            SetMaxCellsHeader();
            auditLogService.LogAuditEvent();
            return Ok();
        }

        /// <summary>
        /// HEAD endpoint returning only headers (no body) for the data query target. Validates existence and language availability.
        /// </summary>
        /// <param name="database">Database identifier containing the table.</param>
        /// <param name="table">PX table identifier.</param>
        /// <param name="lang">Optional language code.</param>
        /// <param name="ct">Cancellation token bound to the client request lifetime.</param>
        /// <response code="200">Resource exists.</response>
        /// <response code="400">Invalid language requested.</response>
        /// <response code="404">Database or table not found.</response>
        [HttpHead("{database}/tables/{table}")]
        [OperationId("headData")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> HeadDataAsync(string database, string table, string? lang = null, CancellationToken ct = default)
        {
            SetMaxCellsHeader();
            try
            {
                DataBaseRef? dbRef = dataSource.GetDataBaseReference(database);
                if (dbRef is null)
                {
                    using (logger.BeginResourceNotFoundScope())
                    {
                        auditLogService.LogAuditEvent();
                        return NotFound();
                    }
                }

                PxFileRef? fileRef = await dataSource.GetFileReferenceCachedAsync(table, dbRef.Value, ct);
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
                    IReadOnlyMatrixMetadata meta = await dataSource.GetMetadataCachedAsync(fileRef.Value, ct);
                    string actualLang = lang ?? meta.DefaultLanguage;
                    if (!meta.AvailableLanguages.Contains(actualLang)) return BadRequest();
                    return Ok();
                }
            }
            catch (ArgumentException argEx)
            {
                logger.LogDebug(argEx, "Argument exception occurred while processing HEAD request: {Message}", argEx.Message);
                return BadRequest(HttpConsts.BAD_REQUEST_PARAMS);
            }
        }

        private void SetMaxCellsHeader()
        {
            long maxSize = AppSettings.Active.QueryLimits.JsonStatMaxCells;
            Response.Headers["X-Max-Cells"] = maxSize.ToString();
        }

        private async Task<ActionResult> GenerateResponse(string database, string table, string? lang, Dictionary<string, Filter> query, CancellationToken ct)
        {
            SetMaxCellsHeader();
            long maxSize = AppSettings.Active.QueryLimits.JsonStatMaxCells;

            DataBaseRef? dbRef = dataSource.GetDataBaseReference(database);
            if (dbRef is null)
            {
                using (logger.BeginResourceNotFoundScope())
                {
                    auditLogService.LogAuditEvent();
                    const string message = "The requested database was not found.";
                    logger.LogDebug(message);
                    return NotFound(message);
                }
            }
            PxFileRef? fileRef = await dataSource.GetFileReferenceCachedAsync(table, dbRef.Value, ct);
            if (fileRef is null)
            {
                using (logger.BeginResourceNotFoundScope(dbRef.Value.Id))
                {
                    auditLogService.LogAuditEvent();
                    const string message = "The requested Px table was not found.";
                    logger.LogDebug(message);
                    return NotFound(message);
                }
            }

            using (logger.BeginResourceScope(dbRef.Value.Id, fileRef.Value.Id))
            {
                auditLogService.LogAuditEvent();
                try
                {
                    IReadOnlyMatrixMetadata meta = await dataSource.GetMetadataCachedAsync(fileRef.Value, ct);

                    string actualLang = lang ?? meta.DefaultLanguage;
                    if (!meta.AvailableLanguages.Contains(actualLang))
                    {
                        const string message = "The content is not available in the requested language.";
                        logger.LogDebug("The Requested language was not available in the table {Table}.", fileRef.Value.Id);
                        return BadRequest(message);
                    }

                    MatrixMap requestMap = MetaFiltering.ApplyToMatrixMeta(meta, query);

                    long size = requestMap.GetSize();
                    if (size > maxSize)
                    {
                        logger.LogInformation("Too large request received. Size: {Size}.", size);
                        return StatusCode(413, $"The request is too large. Please narrow down the query. Maximum size is {maxSize} cells.");
                    }

                    DoubleDataValue[] data = await dataSource.GetDataCachedAsync(fileRef.Value, requestMap, ct);
                    IReadOnlyMatrixMetadata requestMeta = meta.GetTransform(requestMap);
                    DoubleDataValue[] precisionData = DataPrecisionUtils.ApplyContentPrecision(data, requestMeta);

                    // Use proper content negotiation with quality values
                    IList<MediaTypeHeaderValue> acceptHeaderValues = Request.GetTypedHeaders().Accept;
                    string? bestMatch = ContentNegotiation.GetBestMatch(acceptHeaderValues, SupportedMediaTypes);

                    if (bestMatch == TEXT_CSV)
                    {
                        Matrix<DoubleDataValue> requestMatrix = new(requestMeta, precisionData);
                        logger.LogInformation("Data query returned. Returned cell count: {ReturnedCellCount}. Format: {Format}.", precisionData.LongLength, TEXT_CSV);
                        return Content(CsvBuilder.BuildCsvResponse(requestMatrix, actualLang, meta), TEXT_CSV);
                    }
                    if (bestMatch == APPLICATION_JSON)
                    {
                        JsonStat2 jsonStat = JsonStat2Builder.BuildJsonStat2(requestMeta, precisionData, actualLang);
                        logger.LogInformation("Data query returned. Returned cell count: {ReturnedCellCount}. Format: {Format}.", precisionData.LongLength, APPLICATION_JSON);
                        return Ok(jsonStat);
                    }
                }
                catch (BinaryBlobSynchronizationException syncEx)
                {
                    logger.LogInformation(syncEx, "Binary blob data is not yet synchronized for table {Table}.", fileRef.Value.Id);
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "The requested data is temporarily unavailable due to a database update. Please retry shortly.");
                }
                catch (ArgumentException argEx)
                {
                    logger.LogDebug(argEx, "Argument exception occurred while processing request: {Message}", argEx.Message);
                    return BadRequest(HttpConsts.BAD_REQUEST_PARAMS);
                }

                return StatusCode(406);
            }
        }
    }
}
