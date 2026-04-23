using Elastic.Clients.Elasticsearch.Core.Search;
using PxApi.Models.Search;
using PxApi.Services.Search;

namespace PxApi.UnitTests.Services
{
    [TestFixture]
    public class ElasticSearchServiceTests
    {
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