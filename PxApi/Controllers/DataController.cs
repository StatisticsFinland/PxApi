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
using PxApi.Configuration;

namespace PxApi.Controllers
{
    /// <summary>
    /// Provides endpoints for retrieving and querying data in various formats.
    /// </summary>
    /// <param name="dataSource"><see cref="ICachedDataSource"/> instance for accessing data and metadata.</param>"/>
    /// <param name="logger">Logger instance for logging warnings and errors.</param>
    [ApiController]
    [Route("[controller]")]
    public class DataController(ICachedDataSource dataSource, ILogger<DataController> logger) : ControllerBase
    {
        /// <summary>
        /// GET endpoint to receive data in json-stat2 or csv format.
        /// </summary>
        /// <param name="database">The name of the database containing the table.</param>
        /// <param name="table">The name of the px table to query.</param>
        /// <param name="filters">Array of filter specifications in the format 'dimension:filterType=value'.</param>
        /// <param name="lang">Language code for the response. If not provided, uses the default language of the table.</param>
        /// <returns>Data in the format specified by the Accept header.</returns>
        /// <response code="200">Returns the data in the requested format.</response>
        /// <response code="400">If the query parameters are invalid or the requested language is not available.</response>
        /// <response code="404">If the specified database or table is not found.</response>
        /// <response code="406">If the requested media type is not supported.</response>
        [HttpGet("{database}/{table}")]
        [Produces("application/json", "text/csv")]
        [ProducesResponseType(typeof(JsonStat2), 200, "application/json")]
        [ProducesResponseType(typeof(string), 200, "text/csv")]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(406)]
        public async Task<IActionResult> GetDataAsync(
            [FromRoute] string database,
            [FromRoute] string table,
            [FromQuery] string[]? filters = null,
            [FromQuery] string? lang = null
            )
        {
            using (logger.BeginScope(new Dictionary<string, object>()
            {
                { LoggerConsts.CLASS_NAME, nameof(DataController) },
                { LoggerConsts.METHOD_NAME, nameof(GetDataAsync) },
                { LoggerConsts.DB_ID, database },
                { LoggerConsts.PX_FILE, table }
            }))
            {
                try
                {
                    DataBaseRef? dbRef = dataSource.GetDataBaseReference(database);
                    if (dbRef is null)
                    {
                        logger.LogWarning("Database not found.");
                        return NotFound();
                    }
                    PxFileRef? fileRef = await dataSource.GetFileReferenceCachedAsync(table, dbRef.Value);
                    if (fileRef is null)
                    {
                        logger.LogWarning("Px table not found");
                        return NotFound();
                    }

                    Dictionary<string, Filter> filterDict = QueryFilterUtils.ConvertFiltersArrayToFilters(filters ?? []);
                    IReadOnlyMatrixMetadata meta = await dataSource.GetMetadataCachedAsync(fileRef.Value);

                    string actualLang = lang ?? meta.DefaultLanguage;
                    if (!meta.AvailableLanguages.Contains(actualLang))
                    {
                        logger.LogWarning("Requested language {Lang} is not available in the table {Table}.", actualLang, table);
                        return BadRequest("The content is not available in the requested language.");
                    }

                    MatrixMap requestMap = MetaFiltering.ApplyToMatrixMeta(meta, filterDict);

                    long maxSize = AppSettings.Active.QueryLimits.JsonStatMaxCells; // TODO: Add format specific limits
                    int size = requestMap.GetSize();
                    if (size > maxSize)
                    {
                        logger.LogInformation("Too large request received. Size: {Size}.", size);
                        return BadRequest($"The request is too large. Please narrow down the query. Maximum size is {maxSize} cells.");
                    }

                    DoubleDataValue[] data = await dataSource.GetDataCachedAsync(fileRef.Value, requestMap);

                    string acceptHeader = Request.Headers.Accept.ToString();

                    if (acceptHeader.Contains("text/csv"))
                    {
                        // Placeholder for CSV implementation
                        return Content("col1,col2\nval1,val2", "text/csv");
                    }
                    if (string.IsNullOrEmpty(acceptHeader) || acceptHeader.Contains("*/*") || acceptHeader.Contains("application/json"))
                    {
                        JsonStat2 jsonStat = JsonStat2Builder.BuildJsonStat2(meta.GetTransform(requestMap), data, actualLang);
                        return Ok(jsonStat);
                    }

                    return StatusCode(406); // Not Acceptable
                }
                catch (FileNotFoundException notFoundException)
                {
                    logger.LogWarning(notFoundException, "Table or database not found.");
                    return NotFound("Table or database not found");
                }
                catch (ArgumentException ex)
                {
                    logger.LogWarning(ex, "Invalid query parameters provided for table {Table} in database {Database}.", table, database);
                    return BadRequest(ex.Message);
                }
            }
        }

        /// <summary>
        /// POST endpoint to receive data in json-stat2 or csv format.
        /// </summary>
        /// <param name="database">The name of the database containing the table.</param>
        /// <param name="table">The name of the px table to query.</param>
        /// <param name="query">The query parameters for filtering the data in the form of a dictionary, where keys are dimension codes and values are <see cref="Filter"/> objects.</param>
        /// <param name="lang">The language code for the response. If not provided, uses the default language of the table.</param>
        /// <returns>Data in the format specified by the Accept header.</returns>
        /// <response code="200">Returns the data in the requested format.</response>
        /// <response code="400">If the query is invalid or the requested language is not available.</response>
        /// <response code="404">If the specified database or table is not found.</response>
        /// <response code="406">If the requested media type is not supported.</response>
        [HttpPost("{database}/{table}")]
        [Produces("application/json", "text/csv")]
        [ProducesResponseType(typeof(JsonStat2), 200, "application/json")]
        [ProducesResponseType(typeof(string), 200, "text/csv")]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(406)]
        public async Task<IActionResult> PostDataAsync(
            [FromRoute] string database,
            [FromRoute] string table,
            [FromBody] Dictionary<string, Filter> query,
            [FromQuery] string? lang = null
            )
        {
            using (logger.BeginScope(new Dictionary<string, object>()
            {
                { LoggerConsts.CLASS_NAME, nameof(DataController) },
                { LoggerConsts.METHOD_NAME, nameof(PostDataAsync) },
                { LoggerConsts.DB_ID, database },
                { LoggerConsts.PX_FILE, table }
            }))
            {
                try
                {
                    DataBaseRef? dbRef = dataSource.GetDataBaseReference(database);
                    if (dbRef is null)
                    {
                        logger.LogWarning("Database not found.");
                        return NotFound();
                    }
                    PxFileRef? fileRef = await dataSource.GetFileReferenceCachedAsync(table, dbRef.Value);
                    if (fileRef is null)
                    {
                        logger.LogWarning("Px table not found");
                        return NotFound();
                    }

                    IReadOnlyMatrixMetadata meta = await dataSource.GetMetadataCachedAsync(fileRef.Value);

                    // Validate language
                    string actualLang = lang ?? meta.DefaultLanguage;
                    if (!meta.AvailableLanguages.Contains(actualLang))
                    {
                        logger.LogWarning("Requested language {Lang} is not available in the table {Table}.", actualLang, table);
                        return BadRequest("The content is not available in the requested language.");
                    }

                    MatrixMap requestMap = MetaFiltering.ApplyToMatrixMeta(meta, query);

                    long maxSize = AppSettings.Active.QueryLimits.JsonStatMaxCells; // TODO: Add format specific limits
                    int size = requestMap.GetSize();
                    if (size > maxSize)
                    {
                        logger.LogInformation("Too large request received. Size: {Size}.", size);
                        return BadRequest($"The request is too large. Please narrow down the query. Maximum size is {maxSize} cells.");
                    }

                    DoubleDataValue[] data = await dataSource.GetDataCachedAsync(fileRef.Value, requestMap);

                    string acceptHeader = Request.Headers.Accept.ToString();

                    if (acceptHeader.Contains("text/csv"))
                    {
                        // Placeholder for CSV implementation
                        return Content("col1,col2\nval1,val2", "text/csv");
                    }
                    if (string.IsNullOrEmpty(acceptHeader) || acceptHeader.Contains("*/*") || acceptHeader.Contains("application/json"))
                    {
                        JsonStat2 jsonStat = JsonStat2Builder.BuildJsonStat2(meta.GetTransform(requestMap), data, actualLang);
                        return Ok(jsonStat);
                    }
                    
                    return StatusCode(406); // Not Acceptable
                }
                catch (FileNotFoundException notFoundException)
                {
                    logger.LogWarning(notFoundException, "Table or database not found.");
                    return NotFound("Table or database not found");
                }
                catch (ArgumentException ex)
                {
                    logger.LogWarning(ex, "Invalid query parameters provided for table {Table} in database {Database}.", table, database);
                    return BadRequest(ex.Message);
                }
            }
        }
    }
}
