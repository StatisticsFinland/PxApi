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
    /// Provides endpoints for retrieving and querying data in various formats, such as JSON and JSON-stat.
    /// </summary>
    /// <param name="dataSource"><see cref="ICachedDataSource"/> instance for accessing data and metadata.</param>"/>
    /// <param name="logger">Logger instance for logging warnings and errors.</param>
    [ApiController]
    [Route("data")]
    public class DataController(ICachedDataSource dataSource, ILogger<DataController> logger) : ControllerBase
    {
        /// <summary>
        /// GET endpoint to receive px cube data in <see cref="DataResponse"/> JSON format.
        /// </summary>
        /// <param name="database">Name of the database containing the table.</param>
        /// <param name="table">Name of the px table to query.</param>
        /// <param name="filters">Array of filter specifications in the format 'dimension:filterType=value'.</param>
        /// <returns>JSON response containing the data, metadata and last updated timestamp.</returns>
        /// <response code="200">Returns the data in <see cref="DataResponse"/> format.</response>
        /// <response code="400">If the query parameters are invalid.</response>
        /// <response code="404">If the specified database or table is not found.</response>
        [HttpGet]
        [Route("{database}/{table}/json")]
        [Produces("application/json")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<DataResponse>> GetJsonAsync(
            [FromRoute] string database,
            [FromRoute] string table,
            [FromQuery] string[]? filters = null
            )
        {
            using (logger.BeginScope(new Dictionary<string, object>()
            {
                { LoggerConsts.CLASS_NAME, nameof(DataController) },
                { LoggerConsts.METHOD_NAME, nameof(GetJsonAsync) },
                { LoggerConsts.DB_ID, database },
                { LoggerConsts.PX_FILE, table }
            }))
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
                MatrixMap requestMap = MetaFiltering.ApplyToMatrixMeta(meta, filterDict);
                
                long maxSize = AppSettings.Active.QueryLimits.JsonMaxCells;
                int size = requestMap.GetSize();
                if(size > maxSize)
                {
                    logger.LogInformation("Too large request received. Size: {Size}.", size);
                    return BadRequest($"The request is too large. Please narrow down the query. Maximum size is {maxSize} cells.");
                }
                DoubleDataValue[] data = await dataSource.GetDataCachedAsync(fileRef.Value, requestMap);

                return Ok(new DataResponse
                {
                    LastUpdated = meta.GetContentDimension().Values.Map(v => v.LastUpdated).Max(),
                    MetaCodes = requestMap,
                    Data = data
                });
            }
        }

        /// <summary>
        /// POST endpoint to receive px cube data in <see cref="DataResponse"/> JSON format.
        /// </summary>
        /// <param name="database">Name of the database containing the table.</param>
        /// <param name="table">Name of the px table to query.</param>
        /// <param name="query">Dictionary containing dimension codes as keys and <see cref="Filter"/> objects as values.</param>
        /// <returns>JSON response containing the data, metadata and last updated timestamp.</returns>
        /// <response code="200">Returns the data in <see cref="DataResponse"/> format.</response>
        /// <response code="400">If the query is invalid.</response>
        /// <response code="404">If the specified database or table is not found.</response>
        [HttpPost]
        [Route("{database}/{table}/json")]
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
            using (logger.BeginScope(new Dictionary<string, object>()
            {
                { LoggerConsts.CLASS_NAME, nameof(DataController) },
                { LoggerConsts.METHOD_NAME, nameof(PostJsonAsync) },
                { LoggerConsts.DB_ID, database },
                { LoggerConsts.PX_FILE, table }
            }))
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
                MatrixMap requestMap = MetaFiltering.ApplyToMatrixMeta(meta, query);
                
                long maxSize = AppSettings.Active.QueryLimits.JsonMaxCells;
                int size = requestMap.GetSize();
                if(size > maxSize)
                {
                    logger.LogInformation("Too large request received. Size: {Size}.", size);
                    return BadRequest($"The request is too large. Please narrow down the query. Maximum size is {maxSize} cells.");
                }
                DoubleDataValue[] data = await dataSource.GetDataCachedAsync(fileRef.Value, requestMap);

                return Ok(new DataResponse
                {
                    LastUpdated = meta.GetContentDimension().Values.Map(v => v.LastUpdated).Max(),
                    MetaCodes = requestMap,
                    Data = data
                });
            }
        }

        /// <summary>
        /// GET endpoint to receive data in json-stat2 format.
        /// </summary>
        /// <param name="database">The name of the database containing the table.</param>
        /// <param name="table">The name of the px table to query.</param>
        /// <param name="filters">Array of filter specifications in the format 'dimension:filterType=value'.</param>
        /// <param name="lang">Language code for the response. If not provided, uses the default language of the table.</param>
        /// <returns>JSON in <see cref="JsonStat2"/> format containing the data and metadata.</returns>
        /// <response code="200">Returns the data in <see cref="JsonStat2"/> format.</response>
        /// <response code="400">If the query parameters are invalid or the requested language is not available.</response>
        /// <response code="404">If the specified database or table is not found.</response>
        [HttpGet]
        [Route("{database}/{table}/json-stat")]
        [Produces("application/json")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<JsonStat2>> GetJsonStatAsync(
            [FromRoute] string database,
            [FromRoute] string table,
            [FromQuery] string[]? filters = null,
            [FromQuery] string? lang = null
            )
        {
            using (logger.BeginScope(new Dictionary<string, object>()
            {
                { LoggerConsts.CLASS_NAME, nameof(DataController) },
                { LoggerConsts.METHOD_NAME, nameof(GetJsonStatAsync) },
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
                    
                    long maxSize = AppSettings.Active.QueryLimits.JsonStatMaxCells;
                    int size = requestMap.GetSize();
                    if(size > maxSize)
                    {
                        logger.LogInformation("Too large request received. Size: {Size}.", size);
                        return BadRequest($"The request is too large. Please narrow down the query. Maximum size is {maxSize} cells.");
                    }
                    
                    DoubleDataValue[] data = await dataSource.GetDataCachedAsync(fileRef.Value, requestMap);
                    JsonStat2 jsonStat = ModelBuilder.BuildJsonStat2(meta.GetTransform(requestMap), data, actualLang);

                    return Ok(jsonStat);
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
        /// POST endpoint to receive data in json-stat2 format.
        /// </summary>
        /// <param name="database">The name of the database containing the table.</param>
        /// <param name="table">The name of the px table to query.</param>
        /// <param name="query">The query parameters for filtering the data in the form of a dictionary, where keys are dimension codes and values are <see cref="Filter"/> objects.</param>
        /// <param name="lang">The language code for the response. If not provided, uses the default language of the table.</param>
        /// <returns>JSON in <see cref="JsonStat2"/> format containing the data and metadata.</returns>
        /// <response code="200">Returns the data in <see cref="JsonStat2"/> format.</response>
        /// <response code="400">If the query is invalid or the requested language is not available.</response>
        /// <response code="404">If the specified database or table is not found.</response>
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
            using (logger.BeginScope(new Dictionary<string, object>()
            {
                { LoggerConsts.CLASS_NAME, nameof(DataController) },
                { LoggerConsts.METHOD_NAME, nameof(PostJsonStatAsync) },
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
                    
                    long maxSize = AppSettings.Active.QueryLimits.JsonStatMaxCells;
                    int size = requestMap.GetSize();
                    if(size > maxSize)
                    {
                        logger.LogInformation("Too large request received. Size: {Size}.", size);
                        return BadRequest($"The request is too large. Please narrow down the query. Maximum size is {maxSize} cells.");
                    }
                    
                    DoubleDataValue[] data = await dataSource.GetDataCachedAsync(fileRef.Value, requestMap);
                    JsonStat2 jsonStat = ModelBuilder.BuildJsonStat2(meta.GetTransform(requestMap), data, actualLang);

                    return Ok(jsonStat);
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
