using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement.Mvc;
using Px.Utils.Models.Metadata;
using PxApi.Authentication;
using PxApi.Caching;
using PxApi.Configuration;
using PxApi.Exceptions;
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
    /// Supports hierarchical scoping: global and database-level search.
    /// Gated by the <c>SearchController</c> feature flag; returns 404 when disabled.
    /// </remarks>
    [ApiKeyAuth]
    [FeatureGate(nameof(SearchController))]
    [Route("meta")]
    [ApiController]
    public class SearchController(ISearchService searchService, ICachedDataSource cachedDataSource, ILogger<SearchController> logger, IAuditLogService auditLogger) : ControllerBase
    {
        private const int MAX_PAGE_SIZE = 100;

        /// <summary>
        /// Maximum allowed length for the user-provided search query.
        /// </summary>
        /// <remarks>
        /// This limit bounds untrusted input to keep request validation and downstream search processing predictable.
        /// The value of <c>400</c> allows typical multi-term search phrases while preventing excessively large query strings
        /// from increasing processing overhead or altering API behavior in unexpected ways.
        /// </remarks>
        private const int MAX_QUERY_LENGTH = 400;

        private const string BlankSanitizedQueryMessage = "The query parameter 'q' must contain searchable characters.";

        private static readonly string AcceptedScopeMessage =
            $"Invalid 'scope' value. Accepted values are: {string.Join(", ", Enum.GetNames<SearchTarget>().Select(n => n.ToLowerInvariant()))}";

        /// <summary>
        /// Searches across all databases for tables, dimensions, and values.
        /// </summary>
        /// <param name="q">Search query string.</param>
        /// <param name="scope">Optional search scope (case-insensitive). Accepted values: content (default, searches title/source/note/content variable/used-for), dimension (classificatory variable names), value (classificatory variable values), geo (geographic variable values), all (all fields combined). Returns 400 if the value is provided but not recognized.</param>
        /// <param name="lang">Optional language code (ISO 639-1). Defaults to the configured default language.</param>
        /// <param name="page">Optional 1-based page number, default value is 1.</param>
        /// <param name="pageSize">Optional number of items per page (1-100), default value is 20.</param>
        /// <param name="ct">Cancellation token bound to the client request lifetime.</param>
        /// <returns>Search results with paging information.</returns>
        /// <response code="200">Returns matching search results.</response>
        /// <response code="400">Invalid or missing query parameters.</response>
        /// <response code="500">A matched table contains broken metadata.</response>
        [HttpGet("search")]
        [OperationId("searchGlobal")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(SearchResponse), 200)]
        [ProducesResponseType(typeof(string), 400)]
        [ProducesResponseType(typeof(string), 500)]
        [ProducesResponseType(typeof(string), 503)]
        public async Task<ActionResult<SearchResponse>> SearchAsync(
            [FromQuery] string? q,
            [FromQuery] string? scope = null,
            [FromQuery] string? lang = null,
            [FromQuery][Range(1, int.MaxValue)] int page = 1,
            [FromQuery][Range(1, 100)] int pageSize = 20,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(q)) return BadRequest("The query parameter 'q' is required.");
            if (q.Length > MAX_QUERY_LENGTH) return BadRequest($"Query too long. Maximum length is {MAX_QUERY_LENGTH} characters.");
            if (page < 1 || pageSize < 1) return BadRequest("Invalid paging values.");
            if (pageSize > MAX_PAGE_SIZE) pageSize = MAX_PAGE_SIZE;

            AppSettings settings = AppSettings.Active;
            string actualLang = lang ?? settings.Localization.DefaultLanguage;
            if (!settings.Localization.SupportedLanguages.Contains(actualLang)) return BadRequest("The requested language is not supported.");

            SearchTarget? parsedTarget = ParseScope(scope);
            if (parsedTarget is null) return BadRequest(AcceptedScopeMessage);
            SearchTarget target = parsedTarget.Value;
            string sanitizedQuery = InputSanitizer.SanitizeInput(q, MAX_QUERY_LENGTH);
            if (string.IsNullOrWhiteSpace(sanitizedQuery)) return BadRequest(BlankSanitizedQueryMessage);

            using (logger.BeginSearchScope(sanitizedQuery))
            {
                auditLogger.LogAuditEvent();

                try
                {
                    // Elasticsearch multi_match treats the query as literal text (no query DSL parsing),
                    // so user input is safe from injection. Length is validated above.
                    SearchHitResponse response = await searchService.SearchAsync(sanitizedQuery, target, actualLang, page, pageSize, ct);
                    SearchResponse enrichedResponse = await BuildSearchResponseAsync(response, actualLang, ct);
                    logger.LogInformation("Search completed with {NumOfResults} results.", response.PagingInfo.TotalItems);
                    return Ok(enrichedResponse);
                }
                catch (SearchUnavailableException ex)
                {
                    logger.LogError(ex, "Search backend unavailable.");
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Search is temporarily unavailable.");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Failed to enrich search results.");
                    return StatusCode(StatusCodes.Status500InternalServerError, "A matched table could not be loaded.");
                }
            }
        }

        /// <summary>
        /// Searches within a single database for tables, dimensions, and values.
        /// </summary>
        /// <param name="database">Unique identifier of the database.</param>
        /// <param name="q">Search query string.</param>
        /// <param name="scope">Optional search scope (case-insensitive). Accepted values: content (default, searches title/source/note/content variable/used-for), dimension (classificatory variable names), value (classificatory variable values), geo (geographic variable values), all (all fields combined). Returns 400 if the value is provided but not recognized.</param>
        /// <param name="lang">Optional language code (ISO 639-1). Defaults to the configured default language.</param>
        /// <param name="page">Optional 1-based page number, default value is 1.</param>
        /// <param name="pageSize">Optional number of items per page (1-100), default value is 20.</param>
        /// <param name="ct">Cancellation token bound to the client request lifetime.</param>
        /// <returns>Search results scoped to the database with paging information.</returns>
        /// <response code="200">Returns matching search results.</response>
        /// <response code="400">Invalid or missing query parameters.</response>
        /// <response code="404">Database not found.</response>
        /// <response code="500">A matched table contains broken metadata.</response>
        [HttpGet("databases/{database}/search")]
        [OperationId("searchDatabase")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(SearchResponse), 200)]
        [ProducesResponseType(typeof(string), 400)]
        [ProducesResponseType(typeof(string), 404)]
        [ProducesResponseType(typeof(string), 500)]
        [ProducesResponseType(typeof(string), 503)]
        public async Task<ActionResult<SearchResponse>> SearchDatabaseAsync(
            [FromRoute] string database,
            [FromQuery] string? q,
            [FromQuery] string? scope = null,
            [FromQuery] string? lang = null,
            [FromQuery][Range(1, int.MaxValue)] int page = 1,
            [FromQuery][Range(1, 100)] int pageSize = 20,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(q)) return BadRequest("The query parameter 'q' is required.");
            if (q.Length > MAX_QUERY_LENGTH) return BadRequest($"Query too long. Maximum length is {MAX_QUERY_LENGTH} characters.");
            if (page < 1 || pageSize < 1) return BadRequest("Invalid paging values.");
            if (pageSize > MAX_PAGE_SIZE) pageSize = MAX_PAGE_SIZE;

            AppSettings settings = AppSettings.Active;
            string actualLang = lang ?? settings.Localization.DefaultLanguage;
            if (!settings.Localization.SupportedLanguages.Contains(actualLang)) return BadRequest("The requested language is not supported.");

            SearchTarget? parsedTarget = ParseScope(scope);
            if (parsedTarget is null) return BadRequest(AcceptedScopeMessage);
            SearchTarget target = parsedTarget.Value;
            string sanitizedQuery = InputSanitizer.SanitizeInput(q, MAX_QUERY_LENGTH);
            if (string.IsNullOrWhiteSpace(sanitizedQuery)) return BadRequest(BlankSanitizedQueryMessage);

            DataBaseRef? dbRef = cachedDataSource.GetDataBaseReference(database);
            if (dbRef is null)
            {
                using (logger.BeginDbNotFoundScope())
                {
                    auditLogger.LogAuditEvent();
                    return NotFound("Database not found.");
                }
            }

            using (logger.BeginSearchScope(sanitizedQuery, dbRef.Value.Id))
            {
                auditLogger.LogAuditEvent();

                try
                {
                    // Elasticsearch multi_match treats the query as literal text (no query DSL parsing),
                    // so user input is safe from injection. Length is validated above.
                    SearchHitResponse response = await searchService.SearchDatabaseAsync(dbRef.Value.Id, sanitizedQuery, target, actualLang, page, pageSize, ct);
                    SearchResponse enrichedResponse = await BuildSearchResponseAsync(response, actualLang, ct);
                    logger.LogInformation("Search completed with {NumOfResults} results.", response.PagingInfo.TotalItems);
                    return Ok(enrichedResponse);
                }
                catch (SearchUnavailableException ex)
                {
                    logger.LogError(ex, "Search backend unavailable.");
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Search is temporarily unavailable.");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Failed to enrich search results.");
                    return StatusCode(StatusCodes.Status500InternalServerError, "A matched table could not be loaded.");
                }
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

        private static SearchTarget? ParseScope(string? scope)
        {
            if (string.IsNullOrWhiteSpace(scope)) return SearchTarget.Content;

            if (Enum.TryParse(scope.Trim(), ignoreCase: true, out SearchTarget result) && Enum.IsDefined(result))
            {
                return result;
            }

            return null;
        }

        private async Task<SearchResponse> BuildSearchResponseAsync(SearchHitResponse hitResponse, string lang, CancellationToken ct)
        {
            SearchResultItem[] searchResults = await Task.WhenAll(hitResponse.Results.Select(hit => BuildSearchResultItemAsync(hit, lang, ct)));
            return new SearchResponse
            {
                Query = hitResponse.Query,
                Results = [.. searchResults],
                PagingInfo = hitResponse.PagingInfo
            };
        }

        private async Task<SearchResultItem> BuildSearchResultItemAsync(SearchHit hit, string lang, CancellationToken ct)
        {
            DataBaseRef? dataBaseRef = cachedDataSource.GetDataBaseReference(hit.Database.Id);
            if (dataBaseRef is null)
            {
                throw new InvalidOperationException($"Database '{hit.Database.Id}' was not found while enriching search results.");
            }

            PxFileRef? fileReference = await cachedDataSource.GetFileReferenceCachedAsync(hit.TableId, dataBaseRef.Value, ct);
            if (fileReference is not PxFileRef resolvedFileReference)
            {
                throw new InvalidOperationException($"Table '{hit.TableId}' was not found while enriching search results.");
            }

            IReadOnlyMatrixMetadata metadata = await cachedDataSource.GetMetadataCachedAsync(resolvedFileReference, ct);
            TableSummary summary = TableSummaryBuilder.Build(metadata, resolvedFileReference.Id, lang);
            string rootUrl = AppSettings.Active.RootUrl.ToString().TrimEnd('/');

            return new SearchResultItem
            {
                Score = hit.Score,
                Database = hit.Database,
                Table = summary,
                Matches = hit.Matches,
                Links =
                [
                    new Link
                    {
                        Rel = "metadata",
                        Href = $"{rootUrl}/meta/databases/{hit.Database.Id}/tables/{resolvedFileReference.Id}?lang={lang}",
                        Method = "GET"
                    },
                    new Link
                    {
                        Rel = "data",
                        Href = $"{rootUrl}/data/databases/{hit.Database.Id}/tables/{resolvedFileReference.Id}?lang={lang}",
                        Method = "GET"
                    }
                ]
            };
        }
    }
}
