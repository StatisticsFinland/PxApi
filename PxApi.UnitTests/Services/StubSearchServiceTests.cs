using PxApi.Models.Search;
using PxApi.Services;

namespace PxApi.UnitTests.Services
{
    [TestFixture]
    public class StubSearchServiceTests
    {
        private StubSearchService _service = null!;

        [SetUp]
        public void SetUp()
        {
            _service = new StubSearchService();
        }

        [Test]
        public async Task SearchAsync_ReturnsEmptyResponseWithCorrectPaging()
        {
            // Act
            SearchResponse result = await _service.SearchAsync("test", SearchTarget.Content, "en", 2, 10, CancellationToken.None);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Query.Q, Is.EqualTo("test"));
                Assert.That(result.Query.Target, Is.EqualTo(SearchTarget.Content));
                Assert.That(result.Query.Lang, Is.EqualTo("en"));
                Assert.That(result.Results, Has.Count.EqualTo(0));
                Assert.That(result.PagingInfo.CurrentPage, Is.EqualTo(2));
                Assert.That(result.PagingInfo.PageSize, Is.EqualTo(10));
                Assert.That(result.PagingInfo.TotalItems, Is.EqualTo(0));
            }
        }

        [Test]
        public async Task SearchDatabaseAsync_ReturnsEmptyResponseWithCorrectPaging()
        {
            // Act
            SearchResponse result = await _service.SearchDatabaseAsync("db1", "test", SearchTarget.Dimension, "fi", 1, 20, CancellationToken.None);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Query.Q, Is.EqualTo("test"));
                Assert.That(result.Query.Target, Is.EqualTo(SearchTarget.Dimension));
                Assert.That(result.Query.Lang, Is.EqualTo("fi"));
                Assert.That(result.Results, Is.Empty);
                Assert.That(result.PagingInfo.CurrentPage, Is.EqualTo(1));
                Assert.That(result.PagingInfo.PageSize, Is.EqualTo(20));
                Assert.That(result.PagingInfo.TotalItems, Is.EqualTo(0));
            }
        }

    }
}
