using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Data;
using Px.Utils.Models.Metadata;
using PxApi.Caching;
using PxApi.Configuration;
using PxApi.Controllers;
using PxApi.Models.JsonStat;
using PxApi.Models.QueryFilters;
using PxApi.Models;
using PxApi.UnitTests.ModelBuilderTests;
using Px.Utils.Language;

namespace PxApi.UnitTests.ControllerTests
{
    [TestFixture]
    public class DataControllerTests
    {
        private Mock<ICachedDataSource> _cachedDbConnector = null!;
        private Mock<ILogger<DataController>> _mockLogger = null!;
        private DataController _controller = null!;

        [SetUp]
        public void SetUp()
        {
            _cachedDbConnector = new Mock<ICachedDataSource>();
            _mockLogger = new Mock<ILogger<DataController>>();
            _controller = new DataController(_cachedDbConnector.Object, _mockLogger.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };

            SetupAppSettings();
        }

        private static void SetupAppSettings(uint jsonMaxCells = 0, uint jsonStatMaxCells = 0)
        {
            Dictionary<string, string?> inMemorySettings = new()
            {
                {"RootUrl", "https://testurl.fi"},
                {"DataBases:0:Type", "Mounted"},
                {"DataBases:0:Id", "testdb"},
                {"DataBases:0:CacheConfig:TableList:SlidingExpirationSeconds", "900"},
                {"DataBases:0:CacheConfig:TableList:AbsoluteExpirationSeconds", "900"},
                {"DataBases:0:CacheConfig:Meta:SlidingExpirationSeconds", "900"}, // 15 minutes
                {"DataBases:0:CacheConfig:Meta:AbsoluteExpirationSeconds", "900"}, // 15 minutes 
                {"DataBases:0:CacheConfig:Groupings:SlidingExpirationSeconds", "900"},
                {"DataBases:0:CacheConfig:Groupings:AbsoluteExpirationSeconds", "900"},
                {"DataBases:0:CacheConfig:Data:SlidingExpirationSeconds", "600"}, // 10 minutes
                {"DataBases:0:CacheConfig:Data:AbsoluteExpirationSeconds", "600"}, // 10 minutes
                {"DataBases:0:CacheConfig:Modifiedtime:SlidingExpirationSeconds", "60"},
                {"DataBases:0:CacheConfig:Modifiedtime:AbsoluteExpirationSeconds", "60"},
                {"DataBases:0:Custom:RootPath", "datasource/root/"},
                {"DataBases:0:Custom:ModifiedCheckIntervalMs", "1000"},
                {"DataBases:0:Custom:FileListingCacheDurationMs", "10000"},
            };

            if (jsonMaxCells > 0)
            {
                inMemorySettings["QueryLimits:JsonMaxCells"] = jsonMaxCells.ToString();
            }

            if (jsonStatMaxCells > 0)
            {
                inMemorySettings["QueryLimits:JsonStatMaxCells"] = jsonStatMaxCells.ToString();
            }

            IConfiguration _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            AppSettings.Load(_configuration);
        }

        private void SetupMockDataSourceForValidRequest(string database, string table)
        {
            DataBaseRef dataBaseRef = DataBaseRef.Create(database);
            PxFileRef pxFileRef = PxFileRef.CreateFromPath(Path.Combine("C:", "foo", $"{table}.px"), dataBaseRef);
            IReadOnlyMatrixMetadata mockMetadata = TestMockMetaBuilder.GetMockMetadata();
            DoubleDataValue[] mockData = [
                new DoubleDataValue(1.0, DataValueType.Exists),
                new DoubleDataValue(2.0, DataValueType.Exists)
            ];

            // Added TableGroup list fixture
            List<TableGroup> groupingsFixture = [
                new TableGroup
            {
                Code = "grp1",
                Name = new MultilanguageString([
                    new("fi", "group.fi"),
                    new("sv", "group.sv"),
                    new("en", "group.en")
                ]),
                GroupingCode = "rootGrouping",
                GroupingName = new MultilanguageString([
                    new("fi", "groupingname.fi"),
                    new("sv", "groupingname.sv"),
                    new("en", "groupingname.en")
                ]),
                Links = []
            }
            ];

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns(dataBaseRef);
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(It.Is<string>(s => s == table), dataBaseRef)).ReturnsAsync(pxFileRef);
            _cachedDbConnector.Setup(x => x.GetMetadataCachedAsync(It.IsAny<PxFileRef>())).ReturnsAsync(mockMetadata);
            _cachedDbConnector.Setup(x => x.GetDataCachedAsync(It.IsAny<PxFileRef>(), It.IsAny<MatrixMap>())).ReturnsAsync(mockData);
            _cachedDbConnector.Setup(x => x.GetGroupingsCachedAsync(It.IsAny<PxFileRef>())).ReturnsAsync(groupingsFixture);
        }

        #region GetDataAsync Tests

        [Test]
        public async Task GetDataAsync_ValidRequest_ReturnsOkWithJsonStat2()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            string[] filters = ["dim0-code:code=dim0-value1-code"];
            string lang = "en";
            
            SetupMockDataSourceForValidRequest(database, table);
            _controller.ControllerContext.HttpContext.Request.Headers.Accept = "application/json";

            // Act
            IActionResult result = await _controller.GetDataAsync(database, table, filters, lang);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            OkObjectResult? okResult = result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            JsonStat2? jsonStat = okResult.Value as JsonStat2;
            Assert.That(jsonStat, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(jsonStat.Version, Is.EqualTo("2.0"));
                Assert.That(jsonStat.Class, Is.EqualTo("dataset"));
            });
        }

        [Test]
        public async Task GetDataAsync_ValidRequest_ReturnsOkWithData()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            string[] filters = ["dim0-code:code=dim0-value1-code"];
            string lang = "en";
            
            double[] expectedValues = { 1.0, 2.0 };

            SetupMockDataSourceForValidRequest(database, table);
            _controller.ControllerContext.HttpContext.Request.Headers.Accept = "application/json";

            // Act
            IActionResult result = await _controller.GetDataAsync(database, table, filters, lang);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            OkObjectResult? okResult = result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            JsonStat2? jsonStat = okResult.Value as JsonStat2;
            Assert.That(jsonStat, Is.Not.Null);
            
            var series = jsonStat.Value.Select(v => v.UnsafeValue);
            Assert.That(series, Is.EquivalentTo(expectedValues));
        }

        [Test]
        public async Task GetDataAsync_UnsupportedAcceptHeader_ReturnsNotAcceptable()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            string[] filters = ["dim0-code:code=dim0-value1-code"];
            string lang = "en";

            SetupMockDataSourceForValidRequest(database, table);
            _controller.ControllerContext.HttpContext.Request.Headers.Accept = "text/html";

            // Act
            IActionResult result = await _controller.GetDataAsync(database, table, filters, lang);

            // Assert
            Assert.That(result, Is.InstanceOf<StatusCodeResult>());
            StatusCodeResult? statusResult = result as StatusCodeResult;
            Assert.That(statusResult, Is.Not.Null);
            Assert.That(statusResult.StatusCode, Is.EqualTo(StatusCodes.Status406NotAcceptable));
        }

        [Test]
        public async Task GetDataAsync_MissingTable_ReturnsNotFound()
        {
            // Arrange
            string database = "testdb";
            string table = "nonexistent";
            string[] filters = ["dim0:code=value1"];

            DataBaseRef dataBaseRef = DataBaseRef.Create(database);
            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns(dataBaseRef);
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(It.Is<string>(s => s == table), dataBaseRef)).ReturnsAsync((PxFileRef?)null);

            // Act
            IActionResult result = await _controller.GetDataAsync(database, table, filters);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        }

        #endregion

        #region PostDataAsync Tests

        [Test]
        public async Task PostDataAsync_ValidRequest_ReturnsOkWithJsonStat2()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            Dictionary<string, Filter> query = new() { { "dim0-code", new CodeFilter(["dim0-value1-code"]) } };
            
            SetupMockDataSourceForValidRequest(database, table);
            _controller.ControllerContext.HttpContext.Request.Headers.Accept = "application/json";

            // Act
            IActionResult result = await _controller.PostDataAsync(database, table, query);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            OkObjectResult? okResult = result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            JsonStat2? jsonStat = okResult.Value as JsonStat2;
            Assert.That(jsonStat, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(jsonStat.Version, Is.EqualTo("2.0"));
                Assert.That(jsonStat.Class, Is.EqualTo("dataset"));
            });
        }

        [Test]
        public async Task PostDataAsync_ValidRequest_ReturnsOkWithData()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            Dictionary<string, Filter> query = new() { { "dim0-code", new CodeFilter(["dim0-value1-code"]) } };
            
            double[] expectedValues = { 1.0, 2.0 };

            SetupMockDataSourceForValidRequest(database, table);
            _controller.ControllerContext.HttpContext.Request.Headers.Accept = "application/json";

            // Act
            IActionResult result = await _controller.PostDataAsync(database, table, query);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            OkObjectResult? okResult = result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            JsonStat2? jsonStat = okResult.Value as JsonStat2;
            Assert.That(jsonStat, Is.Not.Null);
            
            var series = jsonStat.Value.Select(v => v.UnsafeValue);
            Assert.That(series, Is.EquivalentTo(expectedValues));
        }

        [Test]
        public async Task PostDataAsync_UnsupportedAcceptHeader_ReturnsNotAcceptable()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            Dictionary<string, Filter> query = new() { { "dim0-code", new CodeFilter(["dim0-value1-code"]) } };
            _controller.ControllerContext.HttpContext.Request.Headers.Accept = "text/html";
            SetupMockDataSourceForValidRequest(database, table);

            // Act
            IActionResult result = await _controller.PostDataAsync(database, table, query);

            // Assert
            Assert.That(result, Is.InstanceOf<StatusCodeResult>());
            StatusCodeResult? statusResult = result as StatusCodeResult;
            Assert.That(statusResult, Is.Not.Null);
            Assert.That(statusResult.StatusCode, Is.EqualTo(StatusCodes.Status406NotAcceptable));
        }

        [Test]
        public async Task PostDataAsync_MissingDatabase_ReturnsNotFound()
        {
            // Arrange
            string database = "nonexistent";
            string table = "testtable";
            Dictionary<string, Filter> query = new() { { "dim0-code", new CodeFilter(["dim0-value1-code"]) } };

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns((DataBaseRef?)null);

            // Act
            IActionResult result = await _controller.PostDataAsync(database, table, query);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        }

        [Test]
        public async Task PostDataAsync_MissingTable_ReturnsNotFound()
        {
            // Arrange
            string database = "testdb";
            string table = "nonexistent";
            Dictionary<string, Filter> query = new() { { "dim0-code", new CodeFilter(["dim0-value1-code"]) } };

            DataBaseRef dataBaseRef = DataBaseRef.Create(database);
            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns(dataBaseRef);
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(It.Is<string>(s => s == table), dataBaseRef)).ReturnsAsync((PxFileRef?)null);

            // Act
            IActionResult result = await _controller.PostDataAsync(database, table, query);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        }

        #endregion

        #region Language Tests

        [Test]
        public async Task GetDataAsync_InvalidLanguage_ReturnsBadRequest()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            string[] filters = ["dim0:code=value1"];
            string lang = "invalid";
            
            SetupMockDataSourceForValidRequest(database, table);

            // Act
            IActionResult result = await _controller.GetDataAsync(database, table, filters, lang);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
            BadRequestObjectResult? badRequest = result as BadRequestObjectResult;
            Assert.That(badRequest, Is.Not.Null);
            Assert.That(badRequest.Value, Is.EqualTo("The content is not available in the requested language."));
        }

        [Test]
        public async Task PostDataAsync_InvalidLanguage_ReturnsBadRequest()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            Dictionary<string, Filter> query = new() { { "dim0-code", new CodeFilter(["dim0-value1-code"]) } };
            string lang = "invalid";
            
            SetupMockDataSourceForValidRequest(database, table);

            // Act
            IActionResult result = await _controller.PostDataAsync(database, table, query, lang);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
            BadRequestObjectResult? badRequest = result as BadRequestObjectResult;
            Assert.That(badRequest, Is.Not.Null);
            Assert.That(badRequest.Value, Is.EqualTo("The content is not available in the requested language."));
        }

        #endregion

        #region Exception Handling Tests

        // TODO: TÄÄ
        [Test]
        public async Task GetDataAsync_FileNotFound_ReturnsNotFound()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            string[] filters = ["dim0:code=value1"];
            
            DataBaseRef dataBaseRef = DataBaseRef.Create(database);
            PxFileRef? pxFileRef = null;

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns(dataBaseRef);
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(It.Is<string>(s => s == table), dataBaseRef)).ReturnsAsync(pxFileRef);

            // Act
            IActionResult result = await _controller.GetDataAsync(database, table, filters);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        }

        [Test]
        public async Task GetDataAsync_ArgumentException_ReturnsBadRequest()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            string[] filters = ["dim0:code=value1"];
            string errorMessage = "Invalid argument";
            
            DataBaseRef dataBaseRef = DataBaseRef.Create(database);
            PxFileRef pxFileRef = PxFileRef.CreateFromPath(Path.Combine("C:", "foo", $"{table}.px"), dataBaseRef);

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns(dataBaseRef);
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(It.Is<string>(s => s == table), dataBaseRef)).ReturnsAsync(pxFileRef);
            _cachedDbConnector.Setup(ds => ds.GetMetadataCachedAsync(pxFileRef)).ThrowsAsync(new ArgumentException(errorMessage));

            // Act
            IActionResult result = await _controller.GetDataAsync(database, table, filters);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task PostDataAsync_FileNotFound_ReturnsNotFound()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            Dictionary<string, Filter> query = new() { { "dim0-code", new CodeFilter(["dim0-value1-code"]) } };

            DataBaseRef dataBaseRef = DataBaseRef.Create(database);
            PxFileRef? pxFileRef = null;

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns(dataBaseRef);
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(It.Is<string>(s => s == table), dataBaseRef)).ReturnsAsync(pxFileRef);

            // Act
            IActionResult result = await _controller.PostDataAsync(database, table, query);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        }

        [Test]
        public async Task PostDataAsync_ArgumentException_ReturnsBadRequest()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            Dictionary<string, Filter> query = new() { { "dim0-code", new CodeFilter(["dim0-value1-code"]) } };
            string errorMessage = "Invalid argument";
            
            DataBaseRef dataBaseRef = DataBaseRef.Create(database);
            PxFileRef pxFileRef = PxFileRef.CreateFromPath(Path.Combine("C:", "foo", $"{table}.px"), dataBaseRef);

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns(dataBaseRef);
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(It.Is<string>(s => s == table), dataBaseRef)).ReturnsAsync(pxFileRef);
            _cachedDbConnector.Setup(ds => ds.GetMetadataCachedAsync(pxFileRef)).ThrowsAsync(new ArgumentException(errorMessage));

            // Act
            IActionResult result = await _controller.PostDataAsync(database, table, query);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        #endregion

        #region Query Limits Tests

        [Test]
        public async Task GetDataAsync_RequestExceedsLimit_ReturnsContentTooLarge()
        {
            // Arrange
            const uint limit = 1; 
            SetupAppSettings(jsonStatMaxCells: limit);
            
            string database = "testdb";
            string table = "testtable";
            string[] filters = ["dim0-code:code=dim0-value1-code"];

            SetupMockDataSourceForValidRequest(database, table);
            _controller.ControllerContext.HttpContext.Request.Headers.Accept = "application/json";

            // Act
            IActionResult result = await _controller.GetDataAsync(database, table, filters);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            ObjectResult? badRequest = result as ObjectResult;
            Assert.That(badRequest, Is.Not.Null);
            Assert.That(badRequest.Value, Is.TypeOf<string>());
            Assert.That(badRequest.StatusCode.Equals(413)); // 413 Content Too Large
            string? errorMessage = badRequest.Value as string;
            Assert.That(errorMessage, Does.Contain($"The request is too large. Please narrow down the query. Maximum size is {limit} cells."));
        }

        [Test]
        public async Task PostDataAsync_RequestExceedsLimit_ReturnsContentTooLarge()
        {
            // Arrange
            const uint limit = 1;
            SetupAppSettings(jsonStatMaxCells: limit);
            
            string database = "testdb";
            string table = "testtable";
            Dictionary<string, Filter> query = new() { { "dim0-code", new CodeFilter(["dim0-value1-code"]) } };

            SetupMockDataSourceForValidRequest(database, table);
            _controller.ControllerContext.HttpContext.Request.Headers.Accept = "application/json";

            // Act
            IActionResult result = await _controller.PostDataAsync(database, table, query);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            ObjectResult? tooLarge = result as ObjectResult;
            Assert.That(tooLarge, Is.Not.Null);
            Assert.That(tooLarge.StatusCode.Equals(413)); // 413 Content Too Large
            Assert.That(tooLarge.Value, Is.TypeOf<string>());
            string? errorMessage = tooLarge.Value as string;
            Assert.That(errorMessage, Does.Contain($"The request is too large. Please narrow down the query. Maximum size is {limit} cells."));
        }

        #endregion
    }
}