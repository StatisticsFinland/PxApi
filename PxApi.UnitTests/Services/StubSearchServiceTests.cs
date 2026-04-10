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
            SearchResponse result = await _service.SearchAsync("test", [SearchResultType.Table], "en", 2, 10, CancellationToken.None);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Query.Q, Is.EqualTo("test"));
                Assert.That(result.Query.Types, Is.EqualTo(new List<SearchResultType> { SearchResultType.Table }));
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
            SearchResponse result = await _service.SearchDatabaseAsync("db1", "test", [SearchResultType.Dimension, SearchResultType.Value], "fi", 1, 20, CancellationToken.None);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Query.Q, Is.EqualTo("test"));
                Assert.That(result.Query.Types, Has.Count.EqualTo(2));
                Assert.That(result.Query.Lang, Is.EqualTo("fi"));
                Assert.That(result.Results, Is.Empty);
                Assert.That(result.PagingInfo.CurrentPage, Is.EqualTo(1));
                Assert.That(result.PagingInfo.PageSize, Is.EqualTo(20));
                Assert.That(result.PagingInfo.TotalItems, Is.EqualTo(0));
            }
        }

        [Test]
        public async Task SearchTableAsync_ReturnsEmptyResponseWithCorrectPaging()
        {
            // Act
            SearchResponse result = await _service.SearchTableAsync("db1", "table1", "male", [SearchResultType.Value], "sv", 3, 5, CancellationToken.None);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Query.Q, Is.EqualTo("male"));
                Assert.That(result.Query.Types, Is.EqualTo(new List<SearchResultType> { SearchResultType.Value }));
                Assert.That(result.Query.Lang, Is.EqualTo("sv"));
                Assert.That(result.Results, Is.Empty);
                Assert.That(result.PagingInfo.CurrentPage, Is.EqualTo(3));
                Assert.That(result.PagingInfo.PageSize, Is.EqualTo(5));
                Assert.That(result.PagingInfo.TotalItems, Is.EqualTo(0));
            }
        }
    }
}
