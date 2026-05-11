using Elastic.Clients.Elasticsearch.Core.Search;
using PxApi.Models.Search;
using PxApi.Services.Search;

namespace PxApi.UnitTests.Services
{
    [TestFixture]
    public class ElasticSearchServiceTests
    {
        [TestCase(SearchTarget.Content, new[] { "title", "source", "note", "content_variable", "used_for" })]
        [TestCase(SearchTarget.Dimension, new[] { "classificatory_variable_names" })]
        [TestCase(SearchTarget.Value, new[] { "classificatory_variable_values" })]
        [TestCase(SearchTarget.Geo, new[] { "geo_variable_values" })]
        public void GetFields_KnownTarget_ReturnsExpectedFields(SearchTarget target, string[] expected)
        {
            string[] result = ElasticSearchService.GetFields(target);
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void GetFields_All_ContainsAllFieldGroups()
        {
            string[] result = ElasticSearchService.GetFields(SearchTarget.All);
            Assert.That(result, Is.SupersetOf(new[] { "title", "classificatory_variable_names", "classificatory_variable_values", "geo_variable_values" }));
        }

        [Test]
        public void GetFields_UnknownTarget_ReturnsContentFields()
        {
            string[] result = ElasticSearchService.GetFields((SearchTarget)99);
            Assert.That(result, Is.EqualTo(ElasticSearchService.GetFields(SearchTarget.Content)));
        }

        [TestCase(SearchTarget.Content, new[] { "title^3", "source", "note", "content_variable^3", "used_for^5" })]
        [TestCase(SearchTarget.Dimension, new[] { "classificatory_variable_names" })]
        [TestCase(SearchTarget.Value, new[] { "classificatory_variable_values" })]
        [TestCase(SearchTarget.Geo, new[] { "geo_variable_values" })]
        public void GetQueryFields_KnownTarget_ReturnsExpectedFields(SearchTarget target, string[] expected)
        {
            string[] result = ElasticSearchService.GetQueryFields(target);
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void GetQueryFields_All_ContainsBoostedContentAndUnboostedOtherFields()
        {
            string[] result = ElasticSearchService.GetQueryFields(SearchTarget.All);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Contains.Item("title^3"));
                Assert.That(result, Contains.Item("used_for^5"));
                Assert.That(result, Contains.Item("content_variable^3"));
                Assert.That(result, Contains.Item("classificatory_variable_names"));
                Assert.That(result, Contains.Item("classificatory_variable_values"));
                Assert.That(result, Contains.Item("geo_variable_values"));
            }
        }

        [Test]
        public void GetQueryFields_UnknownTarget_ReturnsContentQueryFields()
        {
            string[] result = ElasticSearchService.GetQueryFields((SearchTarget)99);
            Assert.That(result, Is.EqualTo(ElasticSearchService.GetQueryFields(SearchTarget.Content)));
        }

        [Test]
        public void MapHits_BlankId_SkipsHit()
        {
            // Arrange
            List<Hit<ElasticsearchDocument>> hits =
            [
                new Hit<ElasticsearchDocument>
                {
                    Id = " ",
                    Source = new ElasticsearchDocument
                    {
                        Database = "db1",
                        Title = "Population"
                    }
                }
            ];

            // Act
            List<SearchHit> results = ElasticSearchService.MapHits(hits);

            // Assert
            Assert.That(results, Is.Empty);
        }

        [Test]
        public void MapHits_ValidId_ReturnsSearchHit()
        {
            // Arrange
            List<Hit<ElasticsearchDocument>> hits =
            [
                new Hit<ElasticsearchDocument>
                {
                    Id = "table1",
                    Score = 1.5,
                    Source = new ElasticsearchDocument
                    {
                        Database = "db1",
                        Title = "Population"
                    },
                    Highlight = new Dictionary<string, IReadOnlyCollection<string>>
                    {
                        ["title"] = ["<em>Population</em>"]
                    }
                }
            ];

            // Act
            List<SearchHit> results = ElasticSearchService.MapHits(hits);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(results, Has.Count.EqualTo(1));
                Assert.That(results[0].TableId, Is.EqualTo("table1"));
                Assert.That(results[0].Database.Id, Is.EqualTo("db1"));
                Assert.That(results[0].Matches, Has.Count.EqualTo(1));
                Assert.That(results[0].Matches![0].Path, Is.EqualTo("title"));
            }
        }
    }
}