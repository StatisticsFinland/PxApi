using Elastic.Clients.Elasticsearch.Core.Search;
using PxApi.Models.Search;
using PxApi.Services.Search;

namespace PxApi.UnitTests.Services
{
    [TestFixture]
    public class ElasticSearchServiceTests
    {
        private static readonly string[] AllFieldGroupSample = ["title", "classificatory_variable_names", "classificatory_variable_values", "geo_variable_values"];

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
            Assert.That(result, Is.SupersetOf(AllFieldGroupSample));
        }

        [Test]
        public void GetFields_UnknownTarget_ReturnsContentFields()
        {
            string[] result = ElasticSearchService.GetFields((SearchTarget)99);
            Assert.That(result, Is.EqualTo(ElasticSearchService.GetFields(SearchTarget.Content)));
        }

        [TestCase(SearchTarget.Content)]
        [TestCase(SearchTarget.Dimension)]
        [TestCase(SearchTarget.Value)]
        [TestCase(SearchTarget.Geo)]
        public void GetQueryFields_KnownTarget_FieldNamesMatchGetFields(SearchTarget target)
        {
            string[] queryFields = ElasticSearchService.GetQueryFields(target);
            string[] expectedNames = ElasticSearchService.GetFields(target);
            string[] actualNames = queryFields.Select(f => f.Split('^')[0]).ToArray();
            Assert.That(actualNames, Is.EqualTo(expectedNames));
        }

        [Test]
        public void GetQueryFields_All_FieldNamesMatchGetFieldsAll()
        {
            string[] queryFields = ElasticSearchService.GetQueryFields(SearchTarget.All);
            string[] expectedNames = ElasticSearchService.GetFields(SearchTarget.All);
            string[] actualNames = queryFields.Select(f => f.Split('^')[0]).ToArray();
            Assert.That(actualNames, Is.EqualTo(expectedNames));
        }

        [Test]
        public void GetQueryFields_Content_ContainsBoostedFields()
        {
            string[] queryFields = ElasticSearchService.GetQueryFields(SearchTarget.Content);
            string[] boostedFields = queryFields.Where(f => f.Contains('^')).ToArray();
            Assert.That(boostedFields, Is.Not.Empty);
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