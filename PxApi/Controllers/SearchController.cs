using Microsoft.AspNetCore.Mvc;
using PxApi.Authentication;
using PxApi.Models;
using PxApi.Models.Search;
using PxApi.OpenApi;
using PxApi.Services;
using PxApi.Utilities;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace PxApi.Controllers
{
    /// <summary>
    /// Provides search endpoints for metadata resources.
    /// </summary>
    [ApiKeyAuth]
    [Route("v1/meta")]
    [ApiController]
    public class SearchController(ILogger<SearchController> logger, IAuditLogService auditLogService) : ControllerBase
    {
        /// <summary>
        /// Searches tables by metadata and contained dimensions/values.
        /// </summary>
        /// <response code="200">Search results returned successfully.</response>
        /// <response code="400">Invalid query parameters.</response>
        /// <response code="500">Unexpected server error.</response>
        [HttpGet("tables/search")]
        [OperationId("searchTables")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(SearchResponse<TableListingItem>), 200)]
        [ProducesResponseType(typeof(string), 400)]
        [ProducesResponseType(typeof(string), 500)]
        [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Search implementation pending.")]
        public ActionResult<SearchResponse<TableListingItem>> SearchTables(
            [FromQuery] string? q = null,
            [FromQuery] List<string>? db = null,
            [FromQuery][Range(1, 100)] int limit = 20,
            [FromQuery][Range(0, int.MaxValue)] int offset = 0,
            [FromQuery] string? lang = null,
            [FromQuery] SearchSortOrder? sort = null,
            [FromQuery] string? include = null,
            [FromQuery] List<string>? dimension = null,
            [FromQuery] List<string>? hasValue = null,
            [FromQuery] DateTime? updatedFrom = null,
            [FromQuery] DateTime? updatedTo = null)
        {
            using (logger.BeginScope(new Dictionary<string, object>
            {
                { LoggerConsts.CONTROLLER, nameof(SearchController) },
                { LoggerConsts.ACTION, nameof(SearchTables) }
            }))
            {
                auditLogService.LogAuditEvent();
                SearchResponse<TableListingItem> response = BuildEmptyResponse<TableListingItem>(limit, offset);
                return Ok(response);
            }
        }

        /// <summary>
        /// Searches dimensions by label and/or matching values.
        /// </summary>
        /// <response code="200">Search results returned successfully.</response>
        /// <response code="400">Invalid query parameters.</response>
        /// <response code="500">Unexpected server error.</response>
        [HttpGet("dimensions/search")]
        [OperationId("searchDimensions")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(SearchResponse<DimensionSearchResult>), 200)]
        [ProducesResponseType(typeof(string), 400)]
        [ProducesResponseType(typeof(string), 500)]
        [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Search implementation pending.")]
        public ActionResult<SearchResponse<DimensionSearchResult>> SearchDimensions(
            [FromQuery] string? q = null,
            [FromQuery] List<string>? db = null,
            [FromQuery][Range(1, 100)] int limit = 20,
            [FromQuery][Range(0, int.MaxValue)] int offset = 0,
            [FromQuery] string? lang = null,
            [FromQuery] SearchSortOrder? sort = null,
            [FromQuery] string? include = null,
            [FromQuery] DimensionSearchMatchMode match = DimensionSearchMatchMode.Any,
            [FromQuery] List<string>? tableId = null,
            [FromQuery] List<string>? type = null)
        {
            using (logger.BeginScope(new Dictionary<string, object>
            {
                { LoggerConsts.CONTROLLER, nameof(SearchController) },
                { LoggerConsts.ACTION, nameof(SearchDimensions) }
            }))
            {
                auditLogService.LogAuditEvent();
                SearchResponse<DimensionSearchResult> response = BuildEmptyResponse<DimensionSearchResult>(limit, offset);
                return Ok(response);
            }
        }

        /// <summary>
        /// Searches values across dimensions and optionally by table.
        /// </summary>
        /// <response code="200">Search results returned successfully.</response>
        /// <response code="400">Invalid query parameters.</response>
        /// <response code="500">Unexpected server error.</response>
        [HttpGet("values/search")]
        [OperationId("searchValues")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(SearchResponse<ValueSearchResult>), 200)]
        [ProducesResponseType(typeof(string), 400)]
        [ProducesResponseType(typeof(string), 500)]
        [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Search implementation pending.")]
        public ActionResult<SearchResponse<ValueSearchResult>> SearchValues(
            [FromQuery] string? q = null,
            [FromQuery] List<string>? db = null,
            [FromQuery][Range(1, 100)] int limit = 20,
            [FromQuery][Range(0, int.MaxValue)] int offset = 0,
            [FromQuery] string? lang = null,
            [FromQuery] SearchSortOrder? sort = null,
            [FromQuery] string? include = null,
            [FromQuery] List<string>? dimension = null,
            [FromQuery] List<string>? tableId = null)
        {
            using (logger.BeginScope(new Dictionary<string, object>
            {
                { LoggerConsts.CONTROLLER, nameof(SearchController) },
                { LoggerConsts.ACTION, nameof(SearchValues) }
            }))
            {
                auditLogService.LogAuditEvent();
                SearchResponse<ValueSearchResult> response = BuildEmptyResponse<ValueSearchResult>(limit, offset);
                return Ok(response);
            }
        }

        private static SearchResponse<TItem> BuildEmptyResponse<TItem>(int limit, int offset)
        {
            return new SearchResponse<TItem>
            {
                Results = [],
                PagingInfo = new PagingInfo
                {
                    CurrentPage = 1,
                    PageSize = limit,
                    TotalItems = 0
                }
            };
        }
    }
}
