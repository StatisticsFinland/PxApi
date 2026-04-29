using PxApi.Models.Search;

namespace PxApi.Services
{
    /// <summary>
    /// Abstraction for the search backend. Implementations may use an in-memory stub,
    /// a full-text search engine, or any other index technology.
    /// </summary>
    public interface ISearchService
    {
        /// <summary>
        /// Searches across all databases for tables, dimensions, and values.
        /// </summary>
        /// <param name="query">The search query string.</param>
        /// <param name="target">Search target scope.</param>
        /// <param name="lang">Language code for the search.</param>
        /// <param name="page">1-based page number.</param>
        /// <param name="pageSize">Number of results per page.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A <see cref="SearchHitResponse"/> containing matching hits and paging information.</returns>
        Task<SearchHitResponse> SearchAsync(
            string query,
            SearchTarget target,
            string lang,
            int page,
            int pageSize,
            CancellationToken ct);

        /// <summary>
        /// Verifies that the search backend is reachable and operational.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A task that completes successfully when the backend is healthy.</returns>
        Task CheckHealthAsync(CancellationToken ct);

        /// <summary>
        /// Searches within a single database for tables, dimensions, and values.
        /// </summary>
        /// <param name="databaseId">Identifier of the database to search within.</param>
        /// <param name="query">The search query string.</param>
        /// <param name="target">Search target scope.</param>
        /// <param name="lang">Language code for the search.</param>
        /// <param name="page">1-based page number.</param>
        /// <param name="pageSize">Number of results per page.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A <see cref="SearchHitResponse"/> containing matching hits and paging information.</returns>
        Task<SearchHitResponse> SearchDatabaseAsync(
            string databaseId,
            string query,
            SearchTarget target,
            string lang,
            int page,
            int pageSize,
            CancellationToken ct);
    }
}
