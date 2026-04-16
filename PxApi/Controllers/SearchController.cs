using Microsoft.AspNetCore.Mvc;
using PxApi.Authentication;
using PxApi.Caching;
using PxApi.Configuration;
using PxApi.Models;
using PxApi.Models.Search;
using PxApi.OpenApi;
using PxApi.Services;
using PxApi.Utilities;
using System.ComponentModel.DataAnnotations;

namespace PxApi.Controllers
{
    /// <summary>
    /// Provides metadata search endpoints for discovering tables, dimensions, and values.
    /// </summary>
    /// <remarks>
    /// Supports hierarchical scoping: global, database-level, and table-level search.
    /// </remarks>
    [ApiKeyAuth]
    [Route("meta")]
    [ApiController]
    public class SearchController(ISearchService searchService, ICachedDataSource cachedDataSource, ILogger<SearchController> logger, IAuditLogService auditLogger) : ControllerBase
    {
        private const int MAX_PAGE_SIZE = 100;

        /// <summary>
        /// Searches across all databases for tables, dimensions, and values.
        /// </summary>
        /// <param name="q">Search query string.</param>
        /// <param name="types">Optional search scope: dimension, value, geo, or all. When omitted, searches default content fields (title, source, note, content variable, used-for), not all fields.</param>
        /// <param name="lang">Optional language code (ISO 639-1). Defaults to the configured default language.</param>
        /// <param name="page">Optional 1-based page number, default value is 1.</param>
        /// <param name="pageSize">Optional number of items per page (1-100), default value is 20.</param>
        /// <param name="ct">Cancellation token bound to the client request lifetime.</param>
        /// <returns>Search results with paging information.</returns>
        /// <response code="200">Returns matching search results.</response>
        /// <response code="400">Invalid or missing query parameters.</response>
        [HttpGet("search")]
        [OperationId("searchGlobal")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(SearchResponse), 200)]
        [ProducesResponseType(typeof(string), 400)]
        public async Task<ActionResult<SearchResponse>> SearchAsync(
            [FromQuery] string? q,
            [FromQuery] string? types = null,
            [FromQuery] string? lang = null,
            [FromQuery][Range(1, int.MaxValue)] int page = 1,
            [FromQuery][Range(1, 100)] int pageSize = 20,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(q)) return BadRequest("The query parameter 'q' is required.");
            if (page < 1 || pageSize < 1) return BadRequest("Invalid paging values.");
            if (pageSize > MAX_PAGE_SIZE) pageSize = MAX_PAGE_SIZE;

            AppSettings settings = AppSettings.Active;
            string actualLang = lang ?? settings.Localization.DefaultLanguage;
            if (!settings.Localization.SupportedLanguages.Contains(actualLang)) return BadRequest("The requested language is not supported.");

            SearchTarget target = ParseTypes(types);

            auditLogger.LogAuditEvent();

            SearchResponse response = await searchService.SearchAsync(q, target, actualLang, page, pageSize, ct);
            return Ok(response);
        }

        /// <summary>
        /// Searches within a single database for tables, dimensions, and values.
        /// </summary>
        /// <param name="database">Unique identifier of the database.</param>
        /// <param name="q">Search query string.</param>
        /// <param name="types">Optional search scope: dimension, value, geo, or all. When omitted, searches default content fields (title, source, note, content variable, used-for), not all fields.</param>
        /// <param name="lang">Optional language code (ISO 639-1). Defaults to the configured default language.</param>
        /// <param name="page">Optional 1-based page number, default value is 1.</param>
        /// <param name="pageSize">Optional number of items per page (1-100), default value is 20.</param>
        /// <param name="ct">Cancellation token bound to the client request lifetime.</param>
        /// <returns>Search results scoped to the database with paging information.</returns>
        /// <response code="200">Returns matching search results.</response>
        /// <response code="400">Invalid or missing query parameters.</response>
        /// <response code="404">Database not found.</response>
        [HttpGet("databases/{database}/search")]
        [OperationId("searchDatabase")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(SearchResponse), 200)]
        [ProducesResponseType(typeof(string), 400)]
        [ProducesResponseType(typeof(string), 404)]
        public async Task<ActionResult<SearchResponse>> SearchDatabaseAsync(
            [FromRoute] string database,
            [FromQuery] string? q,
            [FromQuery] string? types = null,
            [FromQuery] string? lang = null,
            [FromQuery][Range(1, int.MaxValue)] int page = 1,
            [FromQuery][Range(1, 100)] int pageSize = 20,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(q)) return BadRequest("The query parameter 'q' is required.");
            if (page < 1 || pageSize < 1) return BadRequest("Invalid paging values.");
            if (pageSize > MAX_PAGE_SIZE) pageSize = MAX_PAGE_SIZE;

            AppSettings settings = AppSettings.Active;
            string actualLang = lang ?? settings.Localization.DefaultLanguage;
            if (!settings.Localization.SupportedLanguages.Contains(actualLang)) return BadRequest("The requested language is not supported.");

            SearchTarget target = ParseTypes(types);

            DataBaseRef? dbRef = cachedDataSource.GetDataBaseReference(database);
            if (dbRef is null)
            {
                using (logger.BeginDbNotFoundScope())
                {
                    auditLogger.LogAuditEvent();
                    return NotFound("Database not found.");
                }
            }

            using (logger.BeginDbScope(dbRef.Value.Id))
            {
                auditLogger.LogAuditEvent();

                SearchResponse response = await searchService.SearchDatabaseAsync(dbRef.Value.Id, q, target, actualLang, page, pageSize, ct);
                return Ok(response);
            }
        }

        /// <summary>
        /// HEAD handler for global search — verifies the endpoint exists.
        /// </summary>
        /// <response code="200">Endpoint exists.</response>
        [HttpHead("search")]
        [OperationId("headSearchGlobal")]
        [ProducesResponseType(200)]
        public IActionResult HeadSearch()
        {
            auditLogger.LogAuditEvent();
            return Ok();
        }

        /// <summary>
        /// Returns allowed HTTP methods for the global search endpoint.
        /// </summary>
        /// <response code="200">Returns allowed methods in the Allow header.</response>
        [HttpOptions("search")]
        [OperationId("optionsSearchGlobal")]
        [ProducesResponseType(200)]
        public IActionResult OptionsSearch()
        {
            Response.Headers.Allow = "GET,HEAD,OPTIONS";
            return Ok();
        }

        /// <summary>
        /// HEAD handler for database-scoped search — verifies the endpoint exists.
        /// </summary>
        /// <param name="database">Unique identifier of the database.</param>
        /// <response code="200">Endpoint exists.</response>
        /// <response code="404">Database not found.</response>
        [HttpHead("databases/{database}/search")]
        [OperationId("headSearchDatabase")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public IActionResult HeadSearchDatabase(string database)
        {
            DataBaseRef? dbRef = cachedDataSource.GetDataBaseReference(database);
            if (dbRef is null)
            {
                using (logger.BeginDbNotFoundScope())
                {
                    auditLogger.LogAuditEvent();
                    return NotFound();
                }
            }

            using (logger.BeginDbScope(dbRef.Value.Id))
            {
                auditLogger.LogAuditEvent();
                return Ok();
            }
        }

        /// <summary>
        /// Returns allowed HTTP methods for the database-scoped search endpoint.
        /// </summary>
        /// <param name="database">Unique identifier of the database.</param>
        /// <response code="200">Returns allowed methods in the Allow header.</response>
        [HttpOptions("databases/{database}/search")]
        [OperationId("optionsSearchDatabase")]
        [ProducesResponseType(200)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Needs to match route signature.")]
        public IActionResult OptionsSearchDatabase(string database)
        {
            Response.Headers.Allow = "GET,HEAD,OPTIONS";
            return Ok();
        }

        private static SearchTarget ParseTypes(string? types)
        {
            if (string.IsNullOrWhiteSpace(types)) return SearchTarget.Content;

            if (Enum.TryParse(types.Trim(), ignoreCase: true, out SearchTarget result) && Enum.IsDefined(result))
            {
                return result;
            }

            return SearchTarget.Content;
        }
    }
}
