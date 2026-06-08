using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Data;
using Px.Utils.Models.Metadata;
using PxApi.Caching;
using PxApi.Controllers;
using PxApi.Models.JsonStat;
using PxApi.Models.QueryFilters;
using PxApi.Models;
using PxApi.Services;
using PxApi.UnitTests.ModelBuilderTests;
using PxApi.UnitTests.Utils;
using PxApi.Exceptions;

namespace PxApi.UnitTests.ControllerTests
{
    [TestFixture]
    public class DataControllerTests
    {
        private Mock<ICachedDataSource> _cachedDbConnector = null!;
        private Mock<ILogger<DataController>> _mockLogger = null!;
        private Mock<IAuditLogService> _mockAuditLogService = null!;
        private DataController _controller = null!;

        [SetUp]
        public void SetUp()
        {
            _cachedDbConnector = new Mock<ICachedDataSource>();
            _mockLogger = new Mock<ILogger<DataController>>();
            _mockAuditLogService = new Mock<IAuditLogService>();
            _controller = new DataController(_cachedDbConnector.Object, _mockLogger.Object, _mockAuditLogService.Object)
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
            Dictionary<string, string?> baseConfig = TestConfigFactory.Base();
            Dictionary<string, string?> mountedDb = TestConfigFactory.MountedDb(0, "testdb", "datasource/root/");
            Dictionary<string, string?> extraDbSettings = new()
            {
                ["DataBases:0:CacheConfig:Modifiedtime:SlidingExpirationSeconds"] = "60",
                ["DataBases:0:CacheConfig:Modifiedtime:AbsoluteExpirationSeconds"] = "60",
                ["DataBases:0:Custom:ModifiedCheckIntervalMs"] = "1000",
                ["DataBases:0:Custom:FileListingCacheDurationMs"] = "10000"
            };

            Dictionary<string, string?> configData = TestConfigFactory.Merge(baseConfig, mountedDb, extraDbSettings);

            if (jsonMaxCells > 0)
            {
                configData["QueryLimits:JsonMaxCells"] = jsonMaxCells.ToString();
            }

            if (jsonStatMaxCells > 0)
            {
                configData["QueryLimits:JsonStatMaxCells"] = jsonStatMaxCells.ToString();
            }

            TestConfigFactory.BuildAndLoad(configData);
        }

        private void SetupMockDataSourceForValidRequest(string database, string table)
        {
            DataBaseRef dataBaseRef = DataBaseRef.Create(database);
            PxFileRef pxFileRef = PxFileRef.ValidateAndCreate(table, dataBaseRef, ["statisticalProgram"]);
            IReadOnlyMatrixMetadata mockMetadata = TestMockMetaBuilder.GetMockMetadata();
            DoubleDataValue[] mockData = [
                new DoubleDataValue(1.0, DataValueType.Exists),
                new DoubleDataValue(2.0, DataValueType.Exists)
            ];

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns(dataBaseRef);
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(It.Is<string>(s => s == table), dataBaseRef, CancellationToken.None)).ReturnsAsync(pxFileRef);
            _cachedDbConnector.Setup(x => x.GetMetadataCachedAsync(It.IsAny<PxFileRef>(), CancellationToken.None)).ReturnsAsync(mockMetadata);
            _cachedDbConnector.Setup(x => x.GetDataCachedAsync(It.IsAny<PxFileRef>(), It.IsAny<MatrixMap>(), CancellationToken.None)).ReturnsAsync(mockData);
        }

        #region GetDataAsync Tests

        [Test]
        public async Task GetDataAsync_ValidRequest_LogsAuditEvent()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            string[] filters = ["dim0-code:code=dim0-value1-code"];
            SetupMockDataSourceForValidRequest(database, table);
            _controller.ControllerContext.HttpContext.Request.Headers.Accept = "application/json";

            // Act
            IActionResult result = await _controller.GetDataAsync(database, table, filters, null);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            _mockAuditLogService.Verify(x => x.LogAuditEvent(), Times.Once);
        }

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
            using (Assert.EnterMultipleScope())
            {
                Assert.That(jsonStat.Version, Is.EqualTo("2.0"));
                Assert.That(jsonStat.Class, Is.EqualTo("dataset"));
            }
        }

        [Test]
        public async Task GetDataAsync_ValidRequest_ReturnsOkWithData()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            string[] filters = ["dim0-code:code=dim0-value1-code"];
            string lang = "en";

            double[] expectedValues = [1.0, 2.0];

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

            IEnumerable<double> series = jsonStat.Value.Select(v => v.UnsafeValue);
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
        public async Task GetDataAsync_MissingDatabase_ReturnsNotFound()
        {
            // Arrange
            string database = "nonexistent";
            string table = "testtable";
            string[] filters = ["dim0:code=value1"];

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns((DataBaseRef?)null);

            // Act
            IActionResult result = await _controller.GetDataAsync(database, table, filters);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        }

        [Test]
        public async Task GetDataAsync_MissingDatabase_LogsAuditEvent()
        {
            // Arrange
            string database = "nonexistent";
            string table = "testtable";
            string[] filters = ["dim0:code=value1"];

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns((DataBaseRef?)null);

            // Act
            await _controller.GetDataAsync(database, table, filters);

            // Assert
            _mockAuditLogService.Verify(x => x.LogAuditEvent(), Times.Once);
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
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(It.Is<string>(s => s == table), dataBaseRef, CancellationToken.None)).ReturnsAsync((PxFileRef?)null);

            // Act
            IActionResult result = await _controller.GetDataAsync(database, table, filters);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        }

        [Test]
        public async Task GetDataAsync_MissingTable_LogsAuditEvent()
        {
            // Arrange
            string database = "testdb";
            string table = "nonexistent";
            string[] filters = ["dim0:code=value1"];

            DataBaseRef dataBaseRef = DataBaseRef.Create(database);
            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns(dataBaseRef);
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(It.Is<string>(s => s == table), dataBaseRef, CancellationToken.None)).ReturnsAsync((PxFileRef?)null);

            // Act
            await _controller.GetDataAsync(database, table, filters);

            // Assert
            _mockAuditLogService.Verify(x => x.LogAuditEvent(), Times.Once);
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
            using (Assert.EnterMultipleScope())
            {
                Assert.That(jsonStat.Version, Is.EqualTo("2.0"));
                Assert.That(jsonStat.Class, Is.EqualTo("dataset"));
            }
        }

        [Test]
        public async Task PostDataAsync_ValidRequest_ReturnsOkWithData()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            Dictionary<string, Filter> query = new() { { "dim0-code", new CodeFilter(["dim0-value1-code"]) } };

            double[] expectedValues = [1.0, 2.0];

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

            IEnumerable<double> series = jsonStat.Value.Select(v => v.UnsafeValue);
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
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(It.Is<string>(s => s == table), dataBaseRef, CancellationToken.None)).ReturnsAsync((PxFileRef?)null);

            // Act
            IActionResult result = await _controller.PostDataAsync(database, table, query);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        }

        [Test]
        public async Task PostDataAsync_ValidRequest_LogsAuditEvent()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            Dictionary<string, Filter> query = new() { { "dim0-code", new CodeFilter(["dim0-value1-code"]) } };

            SetupMockDataSourceForValidRequest(database, table);
            _controller.ControllerContext.HttpContext.Request.Headers.Accept = "application/json";

            // Act
            await _controller.PostDataAsync(database, table, query);

            // Assert
            _mockAuditLogService.Verify(x => x.LogAuditEvent(), Times.Once);
        }

        [Test]
        public async Task PostDataAsync_MissingDatabase_LogsAuditEvent()
        {
            // Arrange
            string database = "nonexistent";
            string table = "testtable";
            Dictionary<string, Filter> query = new() { { "dim0-code", new CodeFilter(["dim0-value1-code"]) } };

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns((DataBaseRef?)null);

            // Act
            await _controller.PostDataAsync(database, table, query);

            // Assert
            _mockAuditLogService.Verify(x => x.LogAuditEvent(), Times.Once);
        }

        [Test]
        public async Task PostDataAsync_MissingTable_LogsAuditEvent()
        {
            // Arrange
            string database = "testdb";
            string table = "nonexistent";
            Dictionary<string, Filter> query = new() { { "dim0-code", new CodeFilter(["dim0-value1-code"]) } };

            DataBaseRef dataBaseRef = DataBaseRef.Create(database);
            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns(dataBaseRef);
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(It.Is<string>(s => s == table), dataBaseRef, CancellationToken.None)).ReturnsAsync((PxFileRef?)null);

            // Act
            await _controller.PostDataAsync(database, table, query);

            // Assert
            _mockAuditLogService.Verify(x => x.LogAuditEvent(), Times.Once);
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
            _controller.ControllerContext.HttpContext.Request.Headers.Accept = "application/json";

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
            _controller.ControllerContext.HttpContext.Request.Headers.Accept = "application/json";

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
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(It.Is<string>(s => s == table), dataBaseRef, CancellationToken.None)).ReturnsAsync(pxFileRef);

            // Accept header not needed since this test expects NotFound before content negotiation
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
            PxFileRef pxFileRef = PxFileRef.ValidateAndCreate(table, dataBaseRef, ["statisticalProgram"]);

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns(dataBaseRef);
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(It.Is<string>(s => s == table), dataBaseRef, CancellationToken.None)).ReturnsAsync(pxFileRef);
            _cachedDbConnector.Setup(ds => ds.GetMetadataCachedAsync(pxFileRef, CancellationToken.None)).ThrowsAsync(new ArgumentException(errorMessage));

            _controller.ControllerContext.HttpContext.Request.Headers.Accept = "application/json";

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
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(It.Is<string>(s => s == table), dataBaseRef, CancellationToken.None)).ReturnsAsync(pxFileRef);

            // Accept header not needed since this test expects NotFound before content negotiation
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
            PxFileRef pxFileRef = PxFileRef.ValidateAndCreate(table, dataBaseRef, ["statisticalProgram"]);

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns(dataBaseRef);
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(It.Is<string>(s => s == table), dataBaseRef, CancellationToken.None)).ReturnsAsync(pxFileRef);
            _cachedDbConnector.Setup(ds => ds.GetMetadataCachedAsync(pxFileRef, CancellationToken.None)).ThrowsAsync(new ArgumentException(errorMessage));

            _controller.ControllerContext.HttpContext.Request.Headers.Accept = "application/json";

            // Act
            IActionResult result = await _controller.PostDataAsync(database, table, query);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task GetDataAsync_BinaryBlobSynchronizationException_ReturnsServiceUnavailable()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            string[] filters = ["dim0-code:code=dim0-value1-code"];

            DataBaseRef dataBaseRef = DataBaseRef.Create(database);
            PxFileRef pxFileRef = PxFileRef.ValidateAndCreate(table, dataBaseRef, ["statisticalProgram"]);
            IReadOnlyMatrixMetadata mockMetadata = TestMockMetaBuilder.GetMockMetadata();
            BinaryBlobSynchronizationException exception = new(pxFileRef, DateTime.UtcNow);

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns(dataBaseRef);
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(It.Is<string>(s => s == table), dataBaseRef, CancellationToken.None)).ReturnsAsync(pxFileRef);
            _cachedDbConnector.Setup(x => x.GetMetadataCachedAsync(It.IsAny<PxFileRef>(), CancellationToken.None)).ReturnsAsync(mockMetadata);
            _cachedDbConnector.Setup(x => x.GetDataCachedAsync(It.IsAny<PxFileRef>(), It.IsAny<MatrixMap>(), CancellationToken.None)).ThrowsAsync(exception);

            _controller.ControllerContext.HttpContext.Request.Headers.Accept = "application/json";

            // Act
            IActionResult result = await _controller.GetDataAsync(database, table, filters);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            ObjectResult? objectResult = result as ObjectResult;
            Assert.That(objectResult, Is.Not.Null);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(objectResult.StatusCode, Is.EqualTo(StatusCodes.Status503ServiceUnavailable));
                Assert.That(objectResult.Value, Is.TypeOf<string>());
                Assert.That(objectResult.Value, Is.EqualTo("The requested data is temporarily unavailable due to a database update. Please retry shortly."));
            }
        }

        [Test]
        public async Task PostDataAsync_BinaryBlobSynchronizationException_ReturnsServiceUnavailable()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            Dictionary<string, Filter> query = new() { { "dim0-code", new CodeFilter(["dim0-value1-code"]) } };

            DataBaseRef dataBaseRef = DataBaseRef.Create(database);
            PxFileRef pxFileRef = PxFileRef.ValidateAndCreate(table, dataBaseRef, ["statisticalProgram"]);
            IReadOnlyMatrixMetadata mockMetadata = TestMockMetaBuilder.GetMockMetadata();
            BinaryBlobSynchronizationException exception = new(pxFileRef, DateTime.UtcNow);

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns(dataBaseRef);
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(It.Is<string>(s => s == table), dataBaseRef, CancellationToken.None)).ReturnsAsync(pxFileRef);
            _cachedDbConnector.Setup(x => x.GetMetadataCachedAsync(It.IsAny<PxFileRef>(), CancellationToken.None)).ReturnsAsync(mockMetadata);
            _cachedDbConnector.Setup(x => x.GetDataCachedAsync(It.IsAny<PxFileRef>(), It.IsAny<MatrixMap>(), CancellationToken.None)).ThrowsAsync(exception);

            _controller.ControllerContext.HttpContext.Request.Headers.Accept = "application/json";

            // Act
            IActionResult result = await _controller.PostDataAsync(database, table, query);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            ObjectResult? objectResult = result as ObjectResult;
            Assert.That(objectResult, Is.Not.Null);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(objectResult.StatusCode, Is.EqualTo(StatusCodes.Status503ServiceUnavailable));
                Assert.That(objectResult.Value, Is.TypeOf<string>());
            }
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
            using (Assert.EnterMultipleScope())
            {
                Assert.That(badRequest.Value, Is.TypeOf<string>());
                Assert.That(badRequest.StatusCode, Is.EqualTo(413)); // 413 Content Too Large
            }
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
            using (Assert.EnterMultipleScope())
            {
                Assert.That(tooLarge.StatusCode, Is.EqualTo(413)); // 413 Content Too Large
                Assert.That(tooLarge.Value, Is.TypeOf<string>());
            }
            string? errorMessage = tooLarge.Value as string;
            Assert.That(errorMessage, Does.Contain($"The request is too large. Please narrow down the query. Maximum size is {limit} cells."));
        }

        #endregion

        #region Content Negotiation Tests

        [Test]
        public async Task GetDataAsync_AcceptWithQualityValues_ReturnsHighestQuality()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            string[] filters = ["dim0-code:code=dim0-value1-code"];

            SetupMockDataSourceForValidRequest(database, table);
            // Simulate: Accept: text/csv;q=0.9, application/json;q=0.5
            _controller.ControllerContext.HttpContext.Request.Headers.Accept = "text/csv;q=0.9, application/json;q=0.5";

            // Act
            IActionResult result = await _controller.GetDataAsync(database, table, filters);

            // Assert
            Assert.That(result, Is.InstanceOf<ContentResult>());
            ContentResult? contentResult = result as ContentResult;
            Assert.That(contentResult, Is.Not.Null);
            Assert.That(contentResult.ContentType, Is.EqualTo("text/csv"));
        }

        [Test]
        public async Task GetDataAsync_AcceptWithDefaultQuality_ReturnsDefaultPreference()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            string[] filters = ["dim0-code:code=dim0-value1-code"];

            SetupMockDataSourceForValidRequest(database, table);
            // Simulate: Accept: application/json, text/csv;q=0.5 (application/json defaults to q=1.0)
            _controller.ControllerContext.HttpContext.Request.Headers.Accept = "application/json, text/csv;q=0.5";

            // Act
            IActionResult result = await _controller.GetDataAsync(database, table, filters);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            OkObjectResult? okResult = result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            JsonStat2? jsonStat = okResult.Value as JsonStat2;
            Assert.That(jsonStat, Is.Not.Null);
        }

        [Test]
        public async Task GetDataAsync_AcceptWithWildcard_ReturnsFirstSupported()
        {
            // Arrange  
            string database = "testdb";
            string table = "testtable";
            string[] filters = ["dim0-code:code=dim0-value1-code"];

            SetupMockDataSourceForValidRequest(database, table);
            // Simulate: Accept: */*
            _controller.ControllerContext.HttpContext.Request.Headers.Accept = "*/*";

            // Act
            IActionResult result = await _controller.GetDataAsync(database, table, filters);

            // Assert - Should return JSON since it's first in SupportedMediaTypes array
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            OkObjectResult? okResult = result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            JsonStat2? jsonStat = okResult.Value as JsonStat2;
            Assert.That(jsonStat, Is.Not.Null);
        }

        [Test]
        public async Task PostDataAsync_AcceptWithQualityValues_ReturnsHighestQuality()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            Dictionary<string, Filter> query = new() { { "dim0-code", new CodeFilter(["dim0-value1-code"]) } };

            SetupMockDataSourceForValidRequest(database, table);
            // Simulate: Accept: text/csv;q=0.9, application/json;q=0.5
            _controller.ControllerContext.HttpContext.Request.Headers.Accept = "text/csv;q=0.9, application/json;q=0.5";

            // Act
            IActionResult result = await _controller.PostDataAsync(database, table, query);

            // Assert
            Assert.That(result, Is.InstanceOf<ContentResult>());
            ContentResult? contentResult = result as ContentResult;
            Assert.That(contentResult, Is.Not.Null);
            Assert.That(contentResult.ContentType, Is.EqualTo("text/csv"));
        }

        [Test]
        public async Task GetDataAsync_AcceptComplexHeader_ReturnsCorrectMatch()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            string[] filters = ["dim0-code:code=dim0-value1-code"];

            SetupMockDataSourceForValidRequest(database, table);
            // Simulate: Accept: application/json, application/xml;q=0.9, */*;q=0.1
            _controller.ControllerContext.HttpContext.Request.Headers.Accept = "application/json, application/xml;q=0.9, */*;q=0.1";

            // Act
            IActionResult result = await _controller.GetDataAsync(database, table, filters);

            // Assert - Should return JSON (highest quality among supported types)
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            OkObjectResult? okResult = result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            JsonStat2? jsonStat = okResult.Value as JsonStat2;
            Assert.That(jsonStat, Is.Not.Null);
        }

        #endregion

        #region Head and Options Endpoint Tests

        [Test]
        public async Task HeadDataAsync_ValidRequest_ReturnsOk()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            SetupMockDataSourceForValidRequest(database, table);
            string lang = "en";

            // Act
            IActionResult result = await _controller.HeadDataAsync(database, table, lang);

            // Assert
            Assert.That(result, Is.InstanceOf<OkResult>());
        }

        [Test]
        public void OptionsData_ReturnsOkAndSetsAllowHeader()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";

            // Act
            IActionResult result = _controller.OptionsData(database, table);

            using (Assert.EnterMultipleScope())
            {
                // Assert
                Assert.That(result, Is.InstanceOf<OkResult>());
                Assert.That(_controller.Response.Headers.Allow, Is.EqualTo("GET,POST,HEAD,OPTIONS"));
            }
        }

        [Test]
        public async Task HeadDataAsync_ValidRequest_ReturnsOk_LogsAudit()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            SetupMockDataSourceForValidRequest(database, table);
            string lang = "en";

            // Act
            IActionResult result = await _controller.HeadDataAsync(database, table, lang);

            // Assert
            Assert.That(result, Is.InstanceOf<OkResult>());
            _mockAuditLogService.Verify(x => x.LogAuditEvent(), Times.Once);
        }

        [Test]
        public void OptionsData_ReturnsOkAndSetsAllowHeader_LogsAudit()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";

            // Act
            IActionResult result = _controller.OptionsData(database, table);

            using (Assert.EnterMultipleScope())
            {
                // Assert
                Assert.That(result, Is.InstanceOf<OkResult>());
                Assert.That(_controller.Response.Headers.Allow, Is.EqualTo("GET,POST,HEAD,OPTIONS"));
            }
            _mockAuditLogService.Verify(x => x.LogAuditEvent(), Times.Once);
        }

        [Test]
        public async Task HeadDataAsync_DatabaseNotFound_ReturnsNotFound()
        {
            // Arrange
            string database = "nonexistent";
            string table = "testtable";

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns((DataBaseRef?)null);

            // Act
            IActionResult result = await _controller.HeadDataAsync(database, table);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundResult>());
        }

        [Test]
        public async Task HeadDataAsync_DatabaseNotFound_LogsAuditEvent()
        {
            // Arrange
            string database = "nonexistent";
            string table = "testtable";

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns((DataBaseRef?)null);

            // Act
            await _controller.HeadDataAsync(database, table);

            // Assert
            _mockAuditLogService.Verify(x => x.LogAuditEvent(), Times.Once);
        }

        [Test]
        public async Task HeadDataAsync_TableNotFound_ReturnsNotFound()
        {
            // Arrange
            string database = "testdb";
            string table = "nonexistent";

            DataBaseRef dataBaseRef = DataBaseRef.Create(database);
            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns(dataBaseRef);
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(It.Is<string>(s => s == table), dataBaseRef, CancellationToken.None)).ReturnsAsync((PxFileRef?)null);

            // Act
            IActionResult result = await _controller.HeadDataAsync(database, table);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundResult>());
        }

        [Test]
        public async Task HeadDataAsync_TableNotFound_LogsAuditEvent()
        {
            // Arrange
            string database = "testdb";
            string table = "nonexistent";

            DataBaseRef dataBaseRef = DataBaseRef.Create(database);
            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns(dataBaseRef);
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(It.Is<string>(s => s == table), dataBaseRef, CancellationToken.None)).ReturnsAsync((PxFileRef?)null);

            // Act
            await _controller.HeadDataAsync(database, table);

            // Assert
            _mockAuditLogService.Verify(x => x.LogAuditEvent(), Times.Once);
        }

        [Test]
        public async Task HeadDataAsync_InvalidLanguage_ReturnsBadRequest()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            string lang = "invalid";

            SetupMockDataSourceForValidRequest(database, table);

            // Act
            IActionResult result = await _controller.HeadDataAsync(database, table, lang);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestResult>());
        }

        #endregion

        #region CSV Tests

        [Test]
        public async Task GetDataAsync_CsvAcceptHeader_ReturnsCsvContent()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            string[] filters = ["dim0-code:code=dim0-value1-code"];
            string lang = "en";

            SetupMockDataSourceForValidRequest(database, table);
            _controller.ControllerContext.HttpContext.Request.Headers.Accept = "text/csv";
            string expected =
                $"\"table-description.en\",\"dim0-value1-name.en dim1-value0-name.en\"{Environment.NewLine}" +
                $"\"content-value0-name.en time-value0-name.en\",1{Environment.NewLine}" +
                $"\"content-value0-name.en time-value1-name.en\",2";

            // Act
            IActionResult result = await _controller.GetDataAsync(database, table, filters, lang);

            // Assert
            Assert.That(result, Is.InstanceOf<ContentResult>());
            ContentResult? contentResult = result as ContentResult;
            Assert.That(contentResult, Is.Not.Null);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(contentResult.ContentType, Is.EqualTo("text/csv"));
                Assert.That(contentResult.Content, Is.Not.Null.And.Not.Empty);
                Assert.That(contentResult.Content, Is.EqualTo(expected));
            }
        }

        [Test]
        public async Task PostDataAsync_CsvAcceptHeader_ReturnsCsvContent()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            Dictionary<string, Filter> query = new() { { "dim0-code", new CodeFilter(["dim0-value1-code"]) } };
            string lang = "en";

            SetupMockDataSourceForValidRequest(database, table);
            _controller.ControllerContext.HttpContext.Request.Headers.Accept = "text/csv";
            string expected =
                $"\"table-description.en\",\"dim0-value1-name.en dim1-value0-name.en\"{Environment.NewLine}" +
                $"\"content-value0-name.en time-value0-name.en\",1{Environment.NewLine}" +
                $"\"content-value0-name.en time-value1-name.en\",2";

            // Act
            IActionResult result = await _controller.PostDataAsync(database, table, query, lang);

            // Assert
            Assert.That(result, Is.InstanceOf<ContentResult>());
            ContentResult? contentResult = result as ContentResult;
            Assert.That(contentResult, Is.Not.Null);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(contentResult.ContentType, Is.EqualTo("text/csv"));
                Assert.That(contentResult.Content, Is.Not.Null.And.Not.Empty);
                Assert.That(contentResult.Content, Is.EqualTo(expected));
            }
        }

        #endregion

        #region Max Cells Header Tests

        [Test]
        public async Task GetDataAsync_ValidRequest_ResponseContainsMaxCellsHeader()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            string[] filters = ["dim0-code:code=dim0-value1-code"];

            SetupMockDataSourceForValidRequest(database, table);
            _controller.ControllerContext.HttpContext.Request.Headers.Accept = "application/json";

            // Act
            await _controller.GetDataAsync(database, table, filters);

            using (Assert.EnterMultipleScope())
            {
                // Assert
                Assert.That(_controller.Response.Headers.ContainsKey("X-Max-Cells"), Is.True);
                Assert.That(_controller.Response.Headers["X-Max-Cells"].ToString(), Is.Not.Empty);
            }
        }

        [Test]
        public async Task PostDataAsync_ValidRequest_ResponseContainsMaxCellsHeader()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            Dictionary<string, Filter> query = new() { { "dim0-code", new CodeFilter(["dim0-value1-code"]) } };

            SetupMockDataSourceForValidRequest(database, table);
            _controller.ControllerContext.HttpContext.Request.Headers.Accept = "application/json";

            // Act
            await _controller.PostDataAsync(database, table, query);

            using (Assert.EnterMultipleScope())
            {
                // Assert
                Assert.That(_controller.Response.Headers.ContainsKey("X-Max-Cells"), Is.True);
                Assert.That(_controller.Response.Headers["X-Max-Cells"].ToString(), Is.Not.Empty);
            }
        }

        [Test]
        public async Task GetDataAsync_RequestExceedsLimit_ResponseContainsMaxCellsHeader()
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
            await _controller.GetDataAsync(database, table, filters);

            using (Assert.EnterMultipleScope())
            {
                // Assert
                Assert.That(_controller.Response.Headers.ContainsKey("X-Max-Cells"), Is.True);
                Assert.That(_controller.Response.Headers["X-Max-Cells"].ToString(), Is.EqualTo(limit.ToString()));
            }
        }

        [Test]
        public async Task HeadDataAsync_ValidRequest_ResponseContainsMaxCellsHeader()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            SetupMockDataSourceForValidRequest(database, table);
            string lang = "en";

            // Act
            await _controller.HeadDataAsync(database, table, lang);

            using (Assert.EnterMultipleScope())
            {
                // Assert
                Assert.That(_controller.Response.Headers.ContainsKey("X-Max-Cells"), Is.True);
                Assert.That(_controller.Response.Headers["X-Max-Cells"].ToString(), Is.Not.Empty);
            }
        }

        [Test]
        public void OptionsData_ResponseContainsMaxCellsHeader()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";

            // Act
            _controller.OptionsData(database, table);

            using (Assert.EnterMultipleScope())
            {
                // Assert
                Assert.That(_controller.Response.Headers.ContainsKey("X-Max-Cells"), Is.True);
                Assert.That(_controller.Response.Headers["X-Max-Cells"].ToString(), Is.Not.Empty);
            }
        }

        #endregion

        #region Cancellation Tests

        [Test]
        public void GetDataAsync_CancellationDuringFileReference_ThrowsOperationCanceledException()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            string[] filters = ["dim0-code:code=dim0-value1-code"];

            using CancellationTokenSource cts = new();
            cts.Cancel();

            DataBaseRef dataBaseRef = DataBaseRef.Create(database);
            _cachedDbConnector.Setup(x => x.GetDataBaseReference(database)).Returns(dataBaseRef);
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(table, dataBaseRef, cts.Token))
                .ThrowsAsync(new OperationCanceledException());

            // Act & Assert
            Assert.That(async () => await _controller.GetDataAsync(database, table, filters, null, cts.Token),
                Throws.InstanceOf<OperationCanceledException>());
        }

        [Test]
        public void PostDataAsync_CancellationDuringFileReference_ThrowsOperationCanceledException()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            Dictionary<string, Filter> query = new() { { "dim0-code", new CodeFilter(["dim0-value1-code"]) } };

            using CancellationTokenSource cts = new();
            cts.Cancel();

            DataBaseRef dataBaseRef = DataBaseRef.Create(database);
            _cachedDbConnector.Setup(x => x.GetDataBaseReference(database)).Returns(dataBaseRef);
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(table, dataBaseRef, cts.Token))
                .ThrowsAsync(new OperationCanceledException());

            // Act & Assert
            Assert.That(async () => await _controller.PostDataAsync(database, table, query, null, cts.Token),
                Throws.InstanceOf<OperationCanceledException>());
        }

        [Test]
        public void GetDataAsync_CancellationDuringGetData_ThrowsOperationCanceledException()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            string[] filters = ["dim0-code:code=dim0-value1-code"];

            using CancellationTokenSource cts = new();
            cts.Cancel();

            DataBaseRef dataBaseRef = DataBaseRef.Create(database);
            PxFileRef pxFileRef = PxFileRef.ValidateAndCreate(table, dataBaseRef, ["statisticalProgram"]);
            IReadOnlyMatrixMetadata mockMetadata = TestMockMetaBuilder.GetMockMetadata();

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(database)).Returns(dataBaseRef);
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(table, dataBaseRef, cts.Token)).ReturnsAsync(pxFileRef);
            _cachedDbConnector.Setup(x => x.GetMetadataCachedAsync(It.IsAny<PxFileRef>(), cts.Token)).ReturnsAsync(mockMetadata);
            _cachedDbConnector.Setup(x => x.GetDataCachedAsync(It.IsAny<PxFileRef>(), It.IsAny<MatrixMap>(), cts.Token))
                .ThrowsAsync(new OperationCanceledException());

            _controller.ControllerContext.HttpContext.Request.Headers.Accept = "application/json";

            // Act & Assert
            Assert.That(async () => await _controller.GetDataAsync(database, table, filters, null, cts.Token),
                Throws.InstanceOf<OperationCanceledException>());
        }

        [Test]
        public void HeadDataAsync_CancellationDuringMetadata_ThrowsOperationCanceledException()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";

            using CancellationTokenSource cts = new();
            cts.Cancel();

            DataBaseRef dataBaseRef = DataBaseRef.Create(database);
            PxFileRef pxFileRef = PxFileRef.ValidateAndCreate(table, dataBaseRef, ["statisticalProgram"]);

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(database)).Returns(dataBaseRef);
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(table, dataBaseRef, cts.Token)).ReturnsAsync(pxFileRef);
            _cachedDbConnector.Setup(x => x.GetMetadataCachedAsync(It.IsAny<PxFileRef>(), cts.Token))
                .ThrowsAsync(new OperationCanceledException());

            // Act & Assert
            Assert.That(async () => await _controller.HeadDataAsync(database, table, null, cts.Token),
                Throws.InstanceOf<OperationCanceledException>());
        }

        #endregion
    }
}