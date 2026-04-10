using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using PxApi.Caching;
using PxApi.Configuration;
using PxApi.Controllers;
using PxApi.Models;
using PxApi.Models.Search;
using PxApi.Services;
using PxApi.UnitTests.Utils;

namespace PxApi.UnitTests.ControllerTests
{
    [TestFixture]
    public class SearchControllerTests
    {
        private Mock<ISearchService> _mockSearchService = null!;
        private Mock<ICachedDataSource> _mockCachedDataSource = null!;
        private Mock<ILogger<SearchController>> _mockLogger = null!;
        private Mock<IAuditLogService> _mockAuditLogger = null!;
        private SearchController _controller = null!;

        [SetUp]
        public void SetUp()
        {
            _mockSearchService = new Mock<ISearchService>();
            _mockCachedDataSource = new Mock<ICachedDataSource>();
            _mockLogger = new Mock<ILogger<SearchController>>();
            _mockAuditLogger = new Mock<IAuditLogService>();

            DataBaseRef db1Ref = DataBaseRef.Create("db1");
            _mockCachedDataSource
                .Setup(x => x.GetDataBaseReference("db1"))
                .Returns(db1Ref);
            _mockCachedDataSource
                .Setup(x => x.GetFileReferenceCachedAsync("table1", db1Ref, It.IsAny<CancellationToken>()))
                .ReturnsAsync(PxFileRef.ValidateAndCreate("table1", db1Ref));

            _controller = new SearchController(_mockSearchService.Object, _mockCachedDataSource.Object, _mockLogger.Object, _mockAuditLogger.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };

            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                TestConfigFactory.MountedDb(0, "db1", "datasource/root1/")
            );
            TestConfigFactory.BuildAndLoad(configData);
        }

        #region Global Search

        [Test]
        public async Task SearchAsync_ValidQuery_ReturnsOkWithSearchResponse()
        {
            // Arrange
            SearchResponse expectedResponse = BuildEmptyResponse("population", [SearchResultType.Table, SearchResultType.Dimension, SearchResultType.Value], "fi");
            _mockSearchService
                .Setup(x => x.SearchAsync("population", It.IsAny<List<SearchResultType>>(), "fi", 1, 20, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            ActionResult<SearchResponse> result = await _controller.SearchAsync("population");

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
                OkObjectResult okResult = (OkObjectResult)result.Result!;
                Assert.That(okResult.Value, Is.InstanceOf<SearchResponse>());
                SearchResponse response = (SearchResponse)okResult.Value!;
                Assert.That(response.Query.Q, Is.EqualTo("population"));
                Assert.That(response.Results, Has.Count.EqualTo(0));
                Assert.That(response.PagingInfo.TotalItems, Is.EqualTo(0));
            }
        }

        [Test]
        public async Task SearchAsync_MissingQuery_ReturnsBadRequest()
        {
            // Act
            ActionResult<SearchResponse> result = await _controller.SearchAsync(null);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task SearchAsync_EmptyQuery_ReturnsBadRequest()
        {
            // Act
            ActionResult<SearchResponse> result = await _controller.SearchAsync("  ");

            // Assert
            Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task SearchAsync_UnsupportedLanguage_ReturnsBadRequest()
        {
            // Act
            ActionResult<SearchResponse> result = await _controller.SearchAsync("test", lang: "de");

            // Assert
            Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task SearchAsync_ValidQuery_LogsAuditEvent()
        {
            // Arrange
            SearchResponse expectedResponse = BuildEmptyResponse("test", [SearchResultType.Table, SearchResultType.Dimension, SearchResultType.Value], "fi");
            _mockSearchService
                .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<List<SearchResultType>>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            await _controller.SearchAsync("test");

            // Assert
            _mockAuditLogger.Verify(x => x.LogAuditEvent(), Times.Once);
        }

        [Test]
        public async Task SearchAsync_DefaultLanguage_UsesConfiguredDefault()
        {
            // Arrange
            SearchResponse expectedResponse = BuildEmptyResponse("test", [SearchResultType.Table, SearchResultType.Dimension, SearchResultType.Value], "fi");
            _mockSearchService
                .Setup(x => x.SearchAsync("test", It.IsAny<List<SearchResultType>>(), "fi", 1, 20, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            ActionResult<SearchResponse> result = await _controller.SearchAsync("test", lang: null);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
            _mockSearchService.Verify(x => x.SearchAsync("test", It.IsAny<List<SearchResultType>>(), "fi", It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task SearchAsync_WithTypesFilter_ParsesCorrectly()
        {
            // Arrange
            SearchResponse expectedResponse = BuildEmptyResponse("test", [SearchResultType.Table], "fi");
            _mockSearchService
                .Setup(x => x.SearchAsync("test", It.Is<List<SearchResultType>>(t => t.Count == 1 && t[0] == SearchResultType.Table), "fi", 1, 20, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            ActionResult<SearchResponse> result = await _controller.SearchAsync("test", types: "table");

            // Assert
            Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
            _mockSearchService.Verify(x => x.SearchAsync("test", It.Is<List<SearchResultType>>(t => t.Count == 1 && t[0] == SearchResultType.Table), "fi", 1, 20, It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Database-scoped Search

        [Test]
        public async Task SearchDatabaseAsync_ValidQuery_ReturnsOkWithSearchResponse()
        {
            // Arrange
            SearchResponse expectedResponse = BuildEmptyResponse("population", [SearchResultType.Table, SearchResultType.Dimension, SearchResultType.Value], "fi");
            _mockSearchService
                .Setup(x => x.SearchDatabaseAsync("db1", "population", It.IsAny<List<SearchResultType>>(), "fi", 1, 20, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            ActionResult<SearchResponse> result = await _controller.SearchDatabaseAsync("db1", "population");

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
                OkObjectResult okResult = (OkObjectResult)result.Result!;
                Assert.That(okResult.Value, Is.InstanceOf<SearchResponse>());
            }
        }

        [Test]
        public async Task SearchDatabaseAsync_MissingQuery_ReturnsBadRequest()
        {
            // Act
            ActionResult<SearchResponse> result = await _controller.SearchDatabaseAsync("db1", null);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task SearchDatabaseAsync_UnsupportedLanguage_ReturnsBadRequest()
        {
            // Act
            ActionResult<SearchResponse> result = await _controller.SearchDatabaseAsync("db1", "test", lang: "de");

            // Assert
            Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task SearchDatabaseAsync_ValidQuery_LogsAuditEvent()
        {
            // Arrange
            SearchResponse expectedResponse = BuildEmptyResponse("test", [SearchResultType.Table, SearchResultType.Dimension, SearchResultType.Value], "fi");
            _mockSearchService
                .Setup(x => x.SearchDatabaseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<SearchResultType>>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            await _controller.SearchDatabaseAsync("db1", "test");

            // Assert
            _mockAuditLogger.Verify(x => x.LogAuditEvent(), Times.Once);
        }

        #endregion

        #region Table-scoped Search

        [Test]
        public async Task SearchTableAsync_ValidQuery_ReturnsOkWithSearchResponse()
        {
            // Arrange
            SearchResponse expectedResponse = BuildEmptyResponse("male", [SearchResultType.Value], "fi");
            _mockSearchService
                .Setup(x => x.SearchTableAsync("db1", "table1", "male", It.IsAny<List<SearchResultType>>(), "fi", 1, 20, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            ActionResult<SearchResponse> result = await _controller.SearchTableAsync("db1", "table1", "male");

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
                OkObjectResult okResult = (OkObjectResult)result.Result!;
                Assert.That(okResult.Value, Is.InstanceOf<SearchResponse>());
            }
        }

        [Test]
        public async Task SearchTableAsync_MissingQuery_ReturnsBadRequest()
        {
            // Act
            ActionResult<SearchResponse> result = await _controller.SearchTableAsync("db1", "table1", null);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task SearchTableAsync_UnsupportedLanguage_ReturnsBadRequest()
        {
            // Act
            ActionResult<SearchResponse> result = await _controller.SearchTableAsync("db1", "table1", "test", lang: "de");

            // Assert
            Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task SearchTableAsync_ValidQuery_LogsAuditEvent()
        {
            // Arrange
            SearchResponse expectedResponse = BuildEmptyResponse("test", [SearchResultType.Table, SearchResultType.Dimension, SearchResultType.Value], "fi");
            _mockSearchService
                .Setup(x => x.SearchTableAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<SearchResultType>>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            await _controller.SearchTableAsync("db1", "table1", "test");

            // Assert
            _mockAuditLogger.Verify(x => x.LogAuditEvent(), Times.Once);
        }

        #endregion

        #region HEAD and OPTIONS

        [Test]
        public void HeadSearch_ReturnsOk()
        {
            // Act
            IActionResult result = _controller.HeadSearch();

            // Assert
            Assert.That(result, Is.InstanceOf<OkResult>());
            _mockAuditLogger.Verify(x => x.LogAuditEvent(), Times.Once);
        }

        [Test]
        public void OptionsSearch_ReturnsAllowHeader()
        {
            // Act
            IActionResult result = _controller.OptionsSearch();

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.InstanceOf<OkResult>());
                Assert.That(_controller.Response.Headers.Allow, Is.EqualTo("GET,HEAD,OPTIONS"));
            }
        }

        [Test]
        public void HeadSearchDatabase_ReturnsOk()
        {
            // Act
            IActionResult result = _controller.HeadSearchDatabase("db1");

            // Assert
            Assert.That(result, Is.InstanceOf<OkResult>());
            _mockAuditLogger.Verify(x => x.LogAuditEvent(), Times.Once);
        }

        [Test]
        public void OptionsSearchDatabase_ReturnsAllowHeader()
        {
            // Act
            IActionResult result = _controller.OptionsSearchDatabase("db1");

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.InstanceOf<OkResult>());
                Assert.That(_controller.Response.Headers.Allow, Is.EqualTo("GET,HEAD,OPTIONS"));
            }
        }

        [Test]
        public async Task HeadSearchTable_ReturnsOk()
        {
            // Act
            IActionResult result = await _controller.HeadSearchTable("db1", "table1");

            // Assert
            Assert.That(result, Is.InstanceOf<OkResult>());
            _mockAuditLogger.Verify(x => x.LogAuditEvent(), Times.Once);
        }

        [Test]
        public void OptionsSearchTable_ReturnsAllowHeader()
        {
            // Act
            IActionResult result = _controller.OptionsSearchTable("db1", "table1");

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.InstanceOf<OkResult>());
                Assert.That(_controller.Response.Headers.Allow, Is.EqualTo("GET,HEAD,OPTIONS"));
            }
        }

        #endregion

        #region Not-found scenarios

        [Test]
        public async Task SearchDatabaseAsync_UnknownDatabase_ReturnsNotFound()
        {
            // Act
            ActionResult<SearchResponse> result = await _controller.SearchDatabaseAsync("unknown", "test");

            // Assert
            Assert.That(result.Result, Is.InstanceOf<NotFoundObjectResult>());
        }

        [Test]
        public async Task SearchTableAsync_UnknownDatabase_ReturnsNotFound()
        {
            // Act
            ActionResult<SearchResponse> result = await _controller.SearchTableAsync("unknown", "table1", "test");

            // Assert
            Assert.That(result.Result, Is.InstanceOf<NotFoundObjectResult>());
        }

        [Test]
        public async Task SearchTableAsync_UnknownTable_ReturnsNotFound()
        {
            // Act
            ActionResult<SearchResponse> result = await _controller.SearchTableAsync("db1", "unknown", "test");

            // Assert
            Assert.That(result.Result, Is.InstanceOf<NotFoundObjectResult>());
        }

        [Test]
        public void HeadSearchDatabase_UnknownDatabase_ReturnsNotFound()
        {
            // Act
            IActionResult result = _controller.HeadSearchDatabase("unknown");

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundResult>());
        }

        [Test]
        public async Task HeadSearchTable_UnknownDatabase_ReturnsNotFound()
        {
            // Act
            IActionResult result = await _controller.HeadSearchTable("unknown", "table1");

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundResult>());
        }

        [Test]
        public async Task HeadSearchTable_UnknownTable_ReturnsNotFound()
        {
            // Act
            IActionResult result = await _controller.HeadSearchTable("db1", "unknown");

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundResult>());
        }

        #endregion

        #region Helpers

        private static SearchResponse BuildEmptyResponse(string query, List<SearchResultType> types, string lang)
        {
            return new SearchResponse
            {
                Query = new SearchQueryInfo
                {
                    Q = query,
                    Types = types,
                    Lang = lang
                },
                Results = [],
                PagingInfo = new PagingInfo
                {
                    CurrentPage = 1,
                    PageSize = 20,
                    TotalItems = 0
                }
            };
        }

        #endregion
    }
}
