using System.Diagnostics.CodeAnalysis;
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
    /// Represents an Elasticsearch field with an optional boost weight for query-time relevance tuning.
    /// </summary>
    /// <param name="Name">The Elasticsearch field name.</param>
    /// <param name="Boost">Optional boost multiplier. When null, no boost is applied.</param>
    internal record BoostedField(string Name, int? Boost = null)
    {
        /// <summary>
        /// Returns the field name with boost notation (e.g. "title^3") if a boost is set, otherwise just the field name.
        /// </summary>
        public string ToQueryField() => Boost.HasValue ? $"{Name}^{Boost.Value}" : Name;
    }

    /// <summary>
    /// Elasticsearch-backed implementation of <see cref="ISearchService"/>.
    /// Translates <see cref="SearchTarget"/> into ES multi_match field sets
    /// and maps hits back to raw search hits for controller-level enrichment.
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "SDK-dependent members are tested indirectly; pure helpers are covered by unit tests.")]
    public class ElasticSearchService(ElasticsearchClient client, SearchConfig searchConfig, ILogger<ElasticSearchService> logger) : ISearchService
    {
        private const string FieldTitle = "title";
        private const string FieldSource = "source";
        private const string FieldNote = "note";
        private const string FieldContentVariable = "content_variable";
        private const string FieldUsedFor = "used_for";
        private const string FieldClassificatoryVariableNames = "classificatory_variable_names";
        private const string FieldClassificatoryVariableValues = "classificatory_variable_values";
        private const string FieldGeoVariableValues = "geo_variable_values";
        private const string FieldDatabase = "database";

        private static readonly BoostedField[] ContentBoostedFields =
        [
            new(FieldTitle),
            new(FieldSource),
            new(FieldNote),
            new(FieldContentVariable, Boost: 2),
            new(FieldUsedFor, Boost: 10)
        ];

        private static readonly BoostedField[] DimensionBoostedFields = [new(FieldClassificatoryVariableNames)];
        private static readonly BoostedField[] ValueBoostedFields = [new(FieldClassificatoryVariableValues)];
        private static readonly BoostedField[] GeoBoostedFields = [new(FieldGeoVariableValues)];
        private static readonly BoostedField[] AllBoostedFields = [.. ContentBoostedFields, .. DimensionBoostedFields, .. ValueBoostedFields, .. GeoBoostedFields];

        private static readonly string[] SourceFields = [FieldDatabase, FieldTitle, FieldNote];



        /// <inheritdoc />
        public async Task CheckHealthAsync(CancellationToken ct)
        {
            // A zero-result search is used instead of client.PingAsync() because Ping
            // hits the cluster-level endpoint which requires monitor privileges that
            // the search API key does not have (returns 403). A size-0 match_all query
            // only needs the read/search index privilege the key already has.
            string index = GetIndexName(AppSettings.Active.Localization.DefaultLanguage);
            SearchResponse<ElasticsearchDocument> response = await client.SearchAsync<ElasticsearchDocument>(s => s
                .Index(index)
                .Size(0)
                .Query(q => q.MatchAll(_ => { })),
                ct);
            if (!response.IsValidResponse)
            {
                throw new SearchUnavailableException("Search backend health check failed.");
            }
        }

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
            string[] queryFields = GetQueryFields(target);
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
                        .Fields(queryFields)
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
            string[] queryFields = GetQueryFields(target);
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
                                .Fields(queryFields)
                            )
                        )
                        .Filter(filter => filter
                            .Term(t => t
                                .Field(new Field(FieldDatabase))
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
            List<SearchHit> results = MapHits(esResponse.Hits);

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

        internal static List<SearchHit> MapHits(IReadOnlyCollection<Hit<ElasticsearchDocument>> hits)
        {
            List<SearchHit> results = [];
            foreach (Hit<ElasticsearchDocument> hit in hits)
            {
                if (hit.Source is null || string.IsNullOrWhiteSpace(hit.Id)) continue;

                ElasticsearchDocument doc = hit.Source;
                List<MatchInfo>? matches = MapHighlights(hit.Highlight);

                SearchHit item = new()
                {
                    Score = hit.Score,
                    Database = new SearchDatabaseRef { Id = doc.Database, Name = doc.Database },
                    TableId = hit.Id,
                    Matches = matches
                };

                results.Add(item);
            }

            return results;
        }

        private static Highlight BuildHighlight(string[] fields)
        {
            return new Highlight
            {
                Fields = fields.ToDictionary(
                    f => new Field(f),
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
            return GetBoostedFields(target).Select(f => f.Name).ToArray();
        }

        internal static string[] GetQueryFields(SearchTarget target)
        {
            return GetBoostedFields(target).Select(f => f.ToQueryField()).ToArray();
        }

        private static BoostedField[] GetBoostedFields(SearchTarget target)
        {
            return target switch
            {
                SearchTarget.Content => ContentBoostedFields,
                SearchTarget.Dimension => DimensionBoostedFields,
                SearchTarget.Value => ValueBoostedFields,
                SearchTarget.Geo => GeoBoostedFields,
                SearchTarget.All => AllBoostedFields,
                _ => ContentBoostedFields
            };
        }

        internal string GetIndexName(string lang)
        {
            return $"{searchConfig.IndexPrefix}-{lang}";
        }
    }
}
