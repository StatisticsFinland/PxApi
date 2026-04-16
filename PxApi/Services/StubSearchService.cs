using PxApi.Models;
using PxApi.Models.Search;

namespace PxApi.Services
{
    /// <summary>
    /// Development stub for <see cref="ISearchService"/> that returns empty result sets.
    /// Replace with a real search index implementation when the backend is available.
    /// </summary>
    public class StubSearchService : ISearchService
    {
        /// <inheritdoc />
        public Task<SearchResponse> SearchAsync(
            string query,
            SearchTarget target,
            string lang,
            int page,
            int pageSize,
            CancellationToken ct)
        {
            SearchResponse response = BuildEmptyResponse(query, target, lang, page, pageSize);
            return Task.FromResult(response);
        }

        /// <inheritdoc />
        public Task<SearchResponse> SearchDatabaseAsync(
            string databaseId,
            string query,
            SearchTarget target,
            string lang,
            int page,
            int pageSize,
            CancellationToken ct)
        {
            SearchResponse response = BuildEmptyResponse(query, target, lang, page, pageSize);
            return Task.FromResult(response);
        }

        private static SearchResponse BuildEmptyResponse(string query, SearchTarget target, string lang, int page, int pageSize)
        {
            return new SearchResponse
            {
                Query = new SearchQueryInfo
                {
                    Q = query,
                    Target = target,
                    Lang = lang
                },
                Results = [],
                PagingInfo = new PagingInfo
                {
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalItems = 0
                }
            };
        }
    }
}
