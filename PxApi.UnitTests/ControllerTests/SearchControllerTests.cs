using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using PxApi.Caching;
using PxApi.Controllers;
using PxApi.Exceptions;
using PxApi.Models;
using PxApi.Models.Search;
using PxApi.Services;
using PxApi.UnitTests.ModelBuilderTests;
using PxApi.UnitTests.Utils;
using PxApi.Utilities;

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
        private DataBaseRef _db1Ref;
        private PxFileRef _table1Ref;

        [SetUp]
        public void SetUp()
        {
            _mockSearchService = new Mock<ISearchService>();
            _mockCachedDataSource = new Mock<ICachedDataSource>();
            _mockLogger = new Mock<ILogger<SearchController>>();
            _mockAuditLogger = new Mock<IAuditLogService>();

            _db1Ref = DataBaseRef.Create("db1");
            _table1Ref = PxFileRef.ValidateAndCreate("table1", _db1Ref);
            _mockCachedDataSource
                .Setup(x => x.GetDataBaseReference("db1"))
                .Returns(_db1Ref);
            _mockCachedDataSource
                .Setup(x => x.GetFileReferenceCachedAsync("table1", _db1Ref, It.IsAny<CancellationToken>()))
                .ReturnsAsync(_table1Ref);

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
            SearchHitResponse expectedResponse = BuildEmptyResponse("population", SearchTarget.Content, "fi");
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
                Assert.That(response.PagingInfo.TotalItems, Is.Zero);
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
            SearchHitResponse expectedResponse = BuildEmptyResponse("test", SearchTarget.Content, "fi");
            _mockSearchService
                .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<SearchTarget>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            await _controller.SearchAsync("test");

            // Assert
            _mockAuditLogger.Verify(x => x.LogAuditEvent(), Times.Once);
        }

        [Test]
        public async Task SearchAsync_ValidQuery_BeginsSearchScopeWithSanitizedQuery()
        {
            // Arrange
            SearchHitResponse expectedResponse = BuildEmptyResponse("populationscript", SearchTarget.Content, "fi");
            _mockSearchService
                .Setup(x => x.SearchAsync("populationscript", It.IsAny<SearchTarget>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            await _controller.SearchAsync("population\n<script>");

            // Assert
            _mockSearchService.Verify(x => x.SearchAsync("populationscript", It.IsAny<SearchTarget>(), "fi", 1, 20, It.IsAny<CancellationToken>()), Times.Once);
            _mockLogger.Verify(x => x.BeginScope(It.Is<It.IsAnyType>((state, _) =>
                MatchesSearchScope(state, "populationscript"))), Times.Once);
        }

        [Test]
        public async Task SearchAsync_DefaultLanguage_UsesConfiguredDefault()
        {
            // Arrange
            SearchHitResponse expectedResponse = BuildEmptyResponse("test", SearchTarget.Content, "fi");
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
            SearchHitResponse expectedResponse = BuildEmptyResponse("test", SearchTarget.Dimension, "fi");
            _mockSearchService
                .Setup(x => x.SearchAsync("test", SearchTarget.Dimension, "fi", 1, 20, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            ActionResult<SearchResponse> result = await _controller.SearchAsync("test", scope: "dimension");

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
            SearchHitResponse expectedResponse = BuildEmptyResponse("population", SearchTarget.Content, "fi");
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
            SearchHitResponse expectedResponse = BuildEmptyResponse("test", SearchTarget.Content, "fi");
            _mockSearchService
                .Setup(x => x.SearchDatabaseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchTarget>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            await _controller.SearchDatabaseAsync("db1", "test");

            // Assert
            _mockAuditLogger.Verify(x => x.LogAuditEvent(), Times.Once);
        }

        [Test]
        public async Task SearchDatabaseAsync_ValidQuery_BeginsSearchScopeWithSanitizedQueryAndDatabase()
        {
            // Arrange
            SearchHitResponse expectedResponse = BuildEmptyResponse("populationscript", SearchTarget.Content, "fi");
            _mockSearchService
                .Setup(x => x.SearchDatabaseAsync("db1", "populationscript", It.IsAny<SearchTarget>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            await _controller.SearchDatabaseAsync("db1", "population\n<script>");

            // Assert
            _mockSearchService.Verify(x => x.SearchDatabaseAsync("db1", "populationscript", It.IsAny<SearchTarget>(), "fi", 1, 20, It.IsAny<CancellationToken>()), Times.Once);
            _mockLogger.Verify(x => x.BeginScope(It.Is<It.IsAnyType>((state, _) =>
                MatchesSearchScope(state, "populationscript", "db1"))), Times.Once);
        }

        [Test]
        public async Task SearchAsync_SearchHit_ReturnsEnrichedSummary()
        {
            // Arrange
            SearchHitResponse expectedResponse = BuildHitResponse("population", SearchTarget.Content, "fi",
            [
                new SearchHit
                {
                    Database = new SearchDatabaseRef { Id = "db1", Name = "db1" },
                    TableId = "table1"
                }
            ]);

            _mockSearchService
                .Setup(x => x.SearchAsync("population", It.IsAny<SearchTarget>(), "fi", 1, 20, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);
            _mockCachedDataSource
                .Setup(x => x.GetMetadataCachedAsync(_table1Ref, It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestMockMetaBuilder.GetMockMetadata());

            // Act
            ActionResult<SearchResponse> result = await _controller.SearchAsync("population");

            // Assert
            OkObjectResult? okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            SearchResponse? response = okResult!.Value as SearchResponse;
            Assert.That(response, Is.Not.Null);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(response!.Results, Has.Count.EqualTo(1));
                Assert.That(response.Results[0].Table.Code, Is.EqualTo("table-tableid"));
                Assert.That(response.Results[0].Table.Name, Is.EqualTo("table-description.fi"));
                Assert.That(response.Results[0].Table.ContentValues, Has.Count.EqualTo(2));
                Assert.That(response.Results[0].Table.TimeRange.From, Is.EqualTo("time-value0-name.fi"));
                Assert.That(response.Results[0].Table.Dimensions, Has.Count.EqualTo(2));
                Assert.That(response.Results[0].Links, Has.Count.EqualTo(2));
            }
        }

        [Test]
        public async Task SearchAsync_MetadataLoadingFails_ReturnsInternalServerError()
        {
            // Arrange
            SearchHitResponse expectedResponse = BuildHitResponse("population", SearchTarget.Content, "fi",
            [
                new SearchHit
                {
                    Database = new SearchDatabaseRef { Id = "db1", Name = "db1" },
                    TableId = "table1"
                }
            ]);

            _mockSearchService
                .Setup(x => x.SearchAsync("population", It.IsAny<SearchTarget>(), "fi", 1, 20, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);
            _mockCachedDataSource
                .Setup(x => x.GetMetadataCachedAsync(_table1Ref, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Broken metadata"));

            // Act
            ActionResult<SearchResponse> result = await _controller.SearchAsync("population");

            // Assert
            ObjectResult? objectResult = result.Result as ObjectResult;
            Assert.That(objectResult, Is.Not.Null);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
                Assert.That(objectResult.Value, Is.EqualTo("A matched table could not be loaded."));
            }
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

        #region Error handling

        [Test]
        public async Task SearchAsync_SearchUnavailable_Returns503WithGenericMessage()
        {
            // Arrange
            _mockSearchService
                .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<SearchTarget>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new SearchUnavailableException("Connection refused"));

            // Act
            ActionResult<SearchResponse> result = await _controller.SearchAsync("test");

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Result, Is.InstanceOf<ObjectResult>());
                ObjectResult objectResult = (ObjectResult)result.Result!;
                Assert.That(objectResult.StatusCode, Is.EqualTo(503));
                Assert.That(objectResult.Value?.ToString(), Does.Not.Contain("Connection refused"));
                Assert.That(objectResult.Value?.ToString(), Is.EqualTo("Search is temporarily unavailable."));
            }
        }

        [Test]
        public async Task SearchDatabaseAsync_SearchUnavailable_Returns503WithGenericMessage()
        {
            // Arrange
            _mockSearchService
                .Setup(x => x.SearchDatabaseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchTarget>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new SearchUnavailableException("Internal error details"));

            // Act
            ActionResult<SearchResponse> result = await _controller.SearchDatabaseAsync("db1", "test");

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Result, Is.InstanceOf<ObjectResult>());
                ObjectResult objectResult = (ObjectResult)result.Result!;
                Assert.That(objectResult.StatusCode, Is.EqualTo(503));
                Assert.That(objectResult.Value?.ToString(), Does.Not.Contain("Internal error details"));
            }
        }

        [Test]
        public async Task SearchAsync_QueryTooLong_ReturnsBadRequest()
        {
            // Arrange
            string longQuery = new('a', 401);

            // Act
            ActionResult<SearchResponse> result = await _controller.SearchAsync(longQuery);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task SearchDatabaseAsync_QueryTooLong_ReturnsBadRequest()
        {
            // Arrange
            string longQuery = new('a', 401);

            // Act
            ActionResult<SearchResponse> result = await _controller.SearchDatabaseAsync("db1", longQuery);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
        }

        #endregion

        #region Helpers

        private static SearchHitResponse BuildEmptyResponse(string query, SearchTarget target, string lang)
        {
            return BuildHitResponse(query, target, lang, []);
        }

        private static SearchHitResponse BuildHitResponse(string query, SearchTarget target, string lang, List<SearchHit> results)
        {
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
                    CurrentPage = 1,
                    PageSize = 20,
                    TotalItems = 0
                }
            };
        }

        private static bool MatchesSearchScope(object state, string expectedQuery, string? expectedDbId = null)
        {
            if (state is not Dictionary<string, object> scopeValues)
            {
                return false;
            }

            if (!scopeValues.TryGetValue(LoggerConsts.SEARCH_QUERY, out object? queryValue) || queryValue is not string actualQuery || actualQuery != expectedQuery)
            {
                return false;
            }

            if (expectedDbId is null)
            {
                return !scopeValues.ContainsKey(LoggerConsts.DB_ID);
            }

            return scopeValues.TryGetValue(LoggerConsts.DB_ID, out object? dbValue) && dbValue is string actualDbId && actualDbId == expectedDbId;
        }

        #endregion
    }
}
