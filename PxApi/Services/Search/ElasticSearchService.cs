using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using PxApi.Configuration;
using PxApi.Exceptions;
using PxApi.Models;
using PxApi.Models.Search;
using MatchType = PxApi.Models.Search.MatchType;

namespace PxApi.Services.Search
{
    /// <summary>
    /// Elasticsearch-backed implementation of <see cref="ISearchService"/>.
    /// Translates <see cref="SearchTarget"/> into ES multi_match field sets
    /// and maps hits back to raw search hits for controller-level enrichment.
    /// </summary>
    public class ElasticSearchService(ElasticsearchClient client, SearchConfig searchConfig, ILogger<ElasticSearchService> logger) : ISearchService
    {
        private static readonly string[] ContentFields = ["title", "source", "note", "content_variable", "used_for"];
        private static readonly string[] DimensionFields = ["classificatory_variable_names"];
        private static readonly string[] ValueFields = ["classificatory_variable_values"];
        private static readonly string[] GeoFields = ["geo_variable_values"];
        private static readonly string[] AllFields = [.. ContentFields, .. DimensionFields, .. ValueFields, .. GeoFields];
        private static readonly string[] SourceFields = ["database", "title", "note"];

        /// <inheritdoc />
        public async Task<SearchHitResponse> SearchAsync(
            string query,
            SearchTarget target,
            string lang,
            int page,
            int pageSize,
            CancellationToken ct)
        {
            string[] fields = GetFields(target);
            string index = GetIndexName(lang);

            SearchRequestDescriptor<ElasticsearchDocument> descriptor = new();
            descriptor
                .Index(index)
                .From((page - 1) * pageSize)
                .Size(pageSize)
                .Source(new SourceConfig(new SourceFilter { Includes = SourceFields }))
                .Query(q => q
                    .MultiMatch(mm => mm
                        .Query(query)
                        .Fields(fields)
                    )
                )
                .Highlight(BuildHighlight(fields));

            return await ExecuteSearchAsync(descriptor, query, target, lang, page, pageSize, ct);
        }

        /// <inheritdoc />
        public async Task<SearchHitResponse> SearchDatabaseAsync(
            string databaseId,
            string query,
            SearchTarget target,
            string lang,
            int page,
            int pageSize,
            CancellationToken ct)
        {
            string[] fields = GetFields(target);
            string index = GetIndexName(lang);

            SearchRequestDescriptor<ElasticsearchDocument> descriptor = new();
            descriptor
                .Index(index)
                .From((page - 1) * pageSize)
                .Size(pageSize)
                .Source(new SourceConfig(new SourceFilter { Includes = SourceFields }))
                .Query(q => q
                    .Bool(b => b
                        .Must(must => must
                            .MultiMatch(mm => mm
                                .Query(query)
                                .Fields(fields)
                            )
                        )
                        .Filter(filter => filter
                            .Term(t => t
                                .Field(new Field("database"))
                                .Value(databaseId)
                            )
                        )
                    )
                )
                .Highlight(BuildHighlight(fields));

            return await ExecuteSearchAsync(descriptor, query, target, lang, page, pageSize, ct);
        }

        private async Task<SearchHitResponse> ExecuteSearchAsync(
            SearchRequestDescriptor<ElasticsearchDocument> descriptor,
            string query,
            SearchTarget target,
            string lang,
            int page,
            int pageSize,
            CancellationToken ct)
        {
            SearchResponse<ElasticsearchDocument> esResponse;
            try
            {
                esResponse = await client.SearchAsync(descriptor, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Elasticsearch request failed");
                throw new SearchUnavailableException("Search backend is temporarily unavailable.", ex);
            }

            if (!esResponse.IsValidResponse)
            {
                logger.LogError("Elasticsearch returned an invalid response: {DebugInformation}", esResponse.DebugInformation);
                throw new SearchUnavailableException("Search backend returned an error.");
            }

            long totalItems = esResponse.HitsMetadata?.Total?.Match(
                totalHits => totalHits.Value,
                value => value) ?? 0;
            List<SearchHit> results = [];
            foreach (Hit<ElasticsearchDocument> hit in esResponse.Hits)
            {
                if (hit.Source is null) continue;

                ElasticsearchDocument doc = hit.Source;
                string tableId = hit.Id ?? string.Empty;

                List<MatchInfo>? matches = MapHighlights(hit.Highlight);

                SearchHit item = new()
                {
                    Score = hit.Score,
                    Database = new SearchDatabaseRef { Id = doc.Database, Name = doc.Database },
                    TableId = tableId,
                    Matches = matches
                };

                results.Add(item);
            }

            return new SearchHitResponse
            {
                Query = new SearchQueryInfo
                {
                    Q = query,
                    Target = target,
                    Lang = lang
                },
                Results = results,
                PagingInfo = new PagingInfo
                {
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalItems = (int)totalItems
                }
            };
        }

        private static Highlight BuildHighlight(string[] fields)
        {
            return new Highlight
            {
                Fields = fields.ToDictionary<string, Field, HighlightField>(
                    f => f!,
                    _ => new HighlightField())
            };
        }

        private static List<MatchInfo>? MapHighlights(IReadOnlyDictionary<string, IReadOnlyCollection<string>>? highlights)
        {
            if (highlights is null || highlights.Count == 0) return null;

            List<MatchInfo> matches = [];
            foreach (KeyValuePair<string, IReadOnlyCollection<string>> entry in highlights)
            {
                foreach (string fragment in entry.Value)
                {
                    matches.Add(new MatchInfo
                    {
                        Path = entry.Key,
                        MatchType = MatchType.Contains,
                        MatchedText = fragment
                    });
                }
            }

            return matches.Count > 0 ? matches : null;
        }

        internal static string[] GetFields(SearchTarget target)
        {
            return target switch
            {
                SearchTarget.Content => ContentFields,
                SearchTarget.Dimension => DimensionFields,
                SearchTarget.Value => ValueFields,
                SearchTarget.Geo => GeoFields,
                SearchTarget.All => AllFields,
                _ => ContentFields
            };
        }

        internal string GetIndexName(string lang)
        {
            return $"{searchConfig.IndexPrefix}-{lang}";
        }
    }
}
