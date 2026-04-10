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
        /// <param name="types">Result types to include (e.g. Table, Dimension, Value). If empty, all types are searched.</param>
        /// <param name="lang">Language code for the search.</param>
        /// <param name="page">1-based page number.</param>
        /// <param name="pageSize">Number of results per page.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A <see cref="SearchResponse"/> containing matching results and paging information.</returns>
        Task<SearchResponse> SearchAsync(
            string query,
            List<SearchResultType> types,
            string lang,
            int page,
            int pageSize,
            CancellationToken ct);

        /// <summary>
        /// Searches within a single database for tables, dimensions, and values.
        /// </summary>
        /// <param name="databaseId">Identifier of the database to search within.</param>
        /// <param name="query">The search query string.</param>
        /// <param name="types">Result types to include. If empty, all types are searched.</param>
        /// <param name="lang">Language code for the search.</param>
        /// <param name="page">1-based page number.</param>
        /// <param name="pageSize">Number of results per page.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A <see cref="SearchResponse"/> containing matching results and paging information.</returns>
        Task<SearchResponse> SearchDatabaseAsync(
            string databaseId,
            string query,
            List<SearchResultType> types,
            string lang,
            int page,
            int pageSize,
            CancellationToken ct);

        /// <summary>
        /// Searches within a specific table for dimensions and values.
        /// </summary>
        /// <param name="databaseId">Identifier of the database containing the table.</param>
        /// <param name="tableId">Identifier of the table to search within.</param>
        /// <param name="query">The search query string.</param>
        /// <param name="types">Result types to include. If empty, all types are searched.</param>
        /// <param name="lang">Language code for the search.</param>
        /// <param name="page">1-based page number.</param>
        /// <param name="pageSize">Number of results per page.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A <see cref="SearchResponse"/> containing matching results and paging information.</returns>
        Task<SearchResponse> SearchTableAsync(
            string databaseId,
            string tableId,
            string query,
            List<SearchResultType> types,
            string lang,
            int page,
            int pageSize,
            CancellationToken ct);
    }
}
