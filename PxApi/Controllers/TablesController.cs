using Microsoft.AspNetCore.Mvc;
using Px.Utils.Models.Metadata;
using PxApi.Caching;
using PxApi.Configuration;
using PxApi.Models;
using PxApi.Utilities;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using PxApi.Services;
using PxApi.OpenApi;
using PxApi.Authentication;

namespace PxApi.Controllers
{
    /// <summary>
    /// Provides endpoints for retrieving tables and their metadata from a specified database.
    /// </summary>
    /// <remarks>
    /// Supports pagination and optional language-based metadata retrieval. Tables are ordered by their PX file name (ascending). If the requested page exceeds the last page an empty list is returned.
    /// </remarks>
    [ApiKeyAuth]
    [Route("meta/databases")]
    [ApiController]
    public class TablesController(ICachedDataSource cachedConnector, ILogger<TablesController> logger, IAuditLogService auditLogger) : ControllerBase
    {
        private const int MAX_PAGE_SIZE = 100;

        /// <summary>
        /// Returns a paged list of tables and their essential metadata for a database.
        /// </summary>
        /// <param name="database">Unique identifier of the database.</param>
        /// <param name="lang">Optional language used to get the metadata. If not provided, the default language is used.</param>
        /// <param name="page">Optional 1-based page number to retrieve, default value is 1.</param>
        /// <param name="pageSize">Optional number of items per page (1-100), default value is 50.</param>
        /// <param name="ct">Cancellation token bound to the client request lifetime.</param>
        /// <returns>Paged list containing table listing items and paging information.</returns>
        /// <response code="200">Returns the table listing.</response>
        /// <response code="400">Invalid query parameter was provided (page &lt; 1, pageSize outside 1-100 or unsupported language).</response>
        /// <response code="404">Database not found.</response>
        /// <response code="500">A table on the requested page contains broken metadata.</response>
        [HttpGet("{database}/tables")]
        [OperationId("listTables")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(PagedTableList), 200)]
        [ProducesResponseType(typeof(string), 400)]
        [ProducesResponseType(typeof(string), 404)]
        [ProducesResponseType(typeof(string), 500)]
        public async Task<ActionResult<PagedTableList>> GetTablesAsync(
            [FromRoute] string database,
            [FromQuery] string? lang = null,
            [FromQuery][Range(1, int.MaxValue)] int page = 1,
            [FromQuery][Range(1, 100)] int pageSize = 50,
            CancellationToken ct = default)
        {
            if (page < 1 || pageSize < 1) return BadRequest("Invalid paging values.");
            if (pageSize > MAX_PAGE_SIZE) pageSize = MAX_PAGE_SIZE;

            AppSettings settings = AppSettings.Active;
            string actualLang = lang ?? settings.Localization.DefaultLanguage;
            if (!settings.Localization.SupportedLanguages.Contains(actualLang)) return BadRequest("The requested language is not supported.");

            try
            {
                DataBaseRef? dataBaseRef = cachedConnector.GetDataBaseReference(database);
                if (dataBaseRef is null)
                {
                    using (logger.BeginDbNotFoundScope())
                    {
                        auditLogger.LogAuditEvent();
                    }
                    return NotFound("Database not found.");
                }

                using (logger.BeginDbScope(dataBaseRef.Value.Id))
                {
                    auditLogger.LogAuditEvent();

                    ImmutableSortedDictionary<string, PxFileRef> tableList = await cachedConnector.GetFileListCachedAsync(dataBaseRef.Value, ct);
                    PagedTableList pagedTableList = new()
                    {
                        Tables = [],
                        PagingInfo = new PagingInfo
                        {
                            CurrentPage = page,
                            PageSize = pageSize,
                            TotalItems = tableList.Count,
                        }
                    };

                    int startIndex = pageSize * (page - 1);
                    int endExclusive = pageSize * page;
                    for (int i = startIndex; i < endExclusive; i++)
                    {
                        if (i >= tableList.Count) break;
                        KeyValuePair<string, PxFileRef> table = tableList.ElementAt(i);

                        try
                        {
                            IReadOnlyMatrixMetadata tableMeta = await cachedConnector.GetMetadataCachedAsync(table.Value, ct);
                            TableSummary summary = TableSummaryBuilder.Build(tableMeta, table.Key, actualLang);

                            Uri fileUri = settings.RootUrl
                                .AddRelativePath("meta", "databases", dataBaseRef.Value.Id, "tables", table.Key)
                                .AddQueryParameters(("lang", actualLang));
                            pagedTableList.Tables.Add(BuildTableListingItem(summary, fileUri));

                        }
                        catch (Exception idReadEx) when (idReadEx is not OperationCanceledException)
                        {
                            logger.LogError(idReadEx, "Failed to build listing summary for table {Table}", table.Key);
                            return StatusCode(StatusCodes.Status500InternalServerError, "A table on the requested page could not be loaded.");
                        }
                    }

                    return Ok(pagedTableList);
                }
            }
            catch (DirectoryNotFoundException dnfe)
            {
                logger.LogInformation(dnfe, "Failed to get tables for database.");
                return NotFound("Database not found.");
            }
        }

        /// <summary>
        /// HEAD endpoint to validate existence of database and optional page parameters without returning body content.
        /// </summary>
        /// <param name="database">Unique identifier of the database.</param>
        /// <param name="page">Optional page number.</param>
        /// <param name="pageSize">Optional page size.</param>
        /// <response code="200">Resource exists.</response>
        /// <response code="400">Invalid paging parameters.</response>
        /// <response code="404">Database not found.</response>
        [HttpHead("{database}/tables")]
        [OperationId("headTables")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public IActionResult HeadTablesAsync(string database, int page = 1, int pageSize = 50)
        {
            if (page < 1 || pageSize < 1 || pageSize > MAX_PAGE_SIZE) return BadRequest();
            DataBaseRef? dataBaseRef = cachedConnector.GetDataBaseReference(database);
            if (dataBaseRef is null)
            {
                using (logger.BeginDbNotFoundScope())
                {
                    auditLogger.LogAuditEvent();
                    return NotFound();
                }
            }

            using (logger.BeginDbScope(dataBaseRef.Value.Id))
            {
                // Audit successful HEAD validation.
                auditLogger.LogAuditEvent();
                return Ok();
            }
        }

        /// <summary>
        /// Returns allowed HTTP methods for the tables resource.
        /// </summary>
        /// <param name="database">Unique identifier of the database.</param>
        /// <response code="200">Returns allowed methods in the Allow header.</response>
        /// <response code="404">Database not found.</response>
        [HttpOptions("{database}/tables")]
        [OperationId("optionsTables")]
        [Produces("application/json")]
        [ProducesResponseType(200)]
        [ProducesResponseType(typeof(string), 404)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Needs to match route signature.")]
        public IActionResult OptionsTables(string database)
        {
            DataBaseRef? dataBaseRef = cachedConnector.GetDataBaseReference(database);
            if (dataBaseRef is null)
            {
                using (logger.BeginDbNotFoundScope())
                {
                    auditLogger.LogAuditEvent();
                }
                return NotFound("Database not found.");
            }

            using (logger.BeginDbScope(dataBaseRef.Value.Id))
            {
                const string methods = "GET,HEAD,OPTIONS";
                Response.Headers.Allow = methods;
                auditLogger.LogAuditEvent();
                return Ok();
            }
        }

        private static TableListingItem BuildTableListingItem(TableSummary summary, Uri uri)
        {
            return new TableListingItem
            {
                Table = summary,
                Links =
                [
                    new Link
                    {
                        Rel = "describedby",
                        Href = uri.ToString(),
                        Method = "GET"
                    }
                ]
            };
        }
    }
}
