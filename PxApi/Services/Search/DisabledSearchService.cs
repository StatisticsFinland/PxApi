using PxApi.Models.Search;

namespace PxApi.Services.Search
{
    /// <summary>
    /// No-op implementation of <see cref="ISearchService"/> registered when the SearchController
    /// feature flag is disabled. This allows the controller to be activated by the DI container
    /// so that the <c>FeatureGate</c> action filter can return 404 as intended.
    /// </summary>
    public class DisabledSearchService : ISearchService
    {
        /// <inheritdoc />
        public Task<SearchHitResponse> SearchAsync(
            string query,
            SearchTarget target,
            string lang,
            int page,
            int pageSize,
            CancellationToken ct)
        {
            throw new NotSupportedException("Search is disabled.");
        }

        /// <inheritdoc />
        public Task<SearchHitResponse> SearchDatabaseAsync(
            string databaseId,
            string query,
            SearchTarget target,
            string lang,
            int page,
            int pageSize,
            CancellationToken ct)
        {
            throw new NotSupportedException("Search is disabled.");
        }
    }
}
