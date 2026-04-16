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
            SearchResponse expectedResponse = BuildEmptyResponse("population", SearchTarget.Content, "fi");
            _mockSearchService
                .Setup(x => x.SearchAsync("population", It.IsAny<SearchTarget>(), "fi", 1, 20, It.IsAny<CancellationToken>()))
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
            SearchResponse expectedResponse = BuildEmptyResponse("test", SearchTarget.Content, "fi");
            _mockSearchService
                .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<SearchTarget>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
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
            SearchResponse expectedResponse = BuildEmptyResponse("test", SearchTarget.Content, "fi");
            _mockSearchService
                .Setup(x => x.SearchAsync("test", It.IsAny<SearchTarget>(), "fi", 1, 20, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            ActionResult<SearchResponse> result = await _controller.SearchAsync("test", lang: null);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
            _mockSearchService.Verify(x => x.SearchAsync("test", It.IsAny<SearchTarget>(), "fi", It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task SearchAsync_WithTypesFilter_ParsesCorrectly()
        {
            // Arrange
            SearchResponse expectedResponse = BuildEmptyResponse("test", SearchTarget.Dimension, "fi");
            _mockSearchService
                .Setup(x => x.SearchAsync("test", SearchTarget.Dimension, "fi", 1, 20, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            ActionResult<SearchResponse> result = await _controller.SearchAsync("test", types: "dimension");

            // Assert
            Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
            _mockSearchService.Verify(x => x.SearchAsync("test", SearchTarget.Dimension, "fi", 1, 20, It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Database-scoped Search

        [Test]
        public async Task SearchDatabaseAsync_ValidQuery_ReturnsOkWithSearchResponse()
        {
            // Arrange
            SearchResponse expectedResponse = BuildEmptyResponse("population", SearchTarget.Content, "fi");
            _mockSearchService
                .Setup(x => x.SearchDatabaseAsync("db1", "population", It.IsAny<SearchTarget>(), "fi", 1, 20, It.IsAny<CancellationToken>()))
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
            SearchResponse expectedResponse = BuildEmptyResponse("test", SearchTarget.Content, "fi");
            _mockSearchService
                .Setup(x => x.SearchDatabaseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchTarget>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            await _controller.SearchDatabaseAsync("db1", "test");

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

        #endregion

        #region Helpers

        private static SearchResponse BuildEmptyResponse(string query, SearchTarget target, string lang)
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
                    CurrentPage = 1,
                    PageSize = 20,
                    TotalItems = 0
                }
            };
        }

        #endregion
    }
}
