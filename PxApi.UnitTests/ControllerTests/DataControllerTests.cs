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
using System.Globalization;

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
                {"DataBases:0:CacheConfig:MaxCacheSize", "1073741824"},
                {"DataBases:0:Custom:RootPath", "datasource/root/"},
                {"DataBases:0:Custom:ModifiedCheckIntervalMs", "1000"},
                {"DataBases:0:Custom:FileListingCacheDurationMs", "10000"},
            };

            IConfiguration _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            AppSettings.Load(_configuration);
        }

        #region GetJsonAsync Tests

        [Test]
        public async Task GetJsonAsync_ValidRequest_ReturnsOkWithDataResponse()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            Dictionary<string, string> parameters = new() { { "dim0.code", "value1" } };
            
            DataBaseRef dataBaseRef = DataBaseRef.Create(database);
            PxFileRef pxFileRef = PxFileRef.Create(table, dataBaseRef);
            IReadOnlyMatrixMetadata mockMetadata = TestMockMetaBuilder.GetMockMetadata();
            DoubleDataValue[] mockData = [
                new DoubleDataValue(1.0, DataValueType.Exists),
                new DoubleDataValue(2.0, DataValueType.Exists)
            ];

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns(dataBaseRef);
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(It.Is<string>(s => s == table), dataBaseRef)).ReturnsAsync(pxFileRef);
            _cachedDbConnector.Setup(x => x.GetMetadataCachedAsync(It.IsAny<PxFileRef>())).ReturnsAsync(mockMetadata);
            _cachedDbConnector.Setup(x => x.GetDataCachedAsync(It.IsAny<PxFileRef>(), It.IsAny<MatrixMap>())).ReturnsAsync(mockData);

            // Act
            ActionResult<DataResponse> result = await _controller.GetJsonAsync(database, table, parameters);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
            OkObjectResult? okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            DataResponse? dataResponse = okResult.Value as DataResponse;
            Assert.That(dataResponse, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(dataResponse.Data, Is.SameAs(mockData));
                Assert.That(dataResponse.LastUpdated, Is.EqualTo(new DateTime(2024, 10, 10, 0, 0, 0, DateTimeKind.Utc)));
            });
        }

        [Test]
        public async Task GetJsonAsync_MissingDatabase_ReturnsNotFound()
        {
            // Arrange
            string database = "nonexistent";
            string table = "testtable";
            Dictionary<string, string> parameters = new() { { "dim0.code", "value1" } };

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns((DataBaseRef?)null);

            // Act
            ActionResult<DataResponse> result = await _controller.GetJsonAsync(database, table, parameters);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<NotFoundResult>());
            _mockLogger.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task GetJsonAsync_MissingTable_ReturnsNotFound()
        {
            // Arrange
            string database = "testdb";
            string table = "nonexistent";
            Dictionary<string, string> parameters = new() { { "dim0.code", "value1" } };

            DataBaseRef dataBaseRef = DataBaseRef.Create(database);
            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns(dataBaseRef);
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(It.Is<string>(s => s == table), dataBaseRef)).ReturnsAsync((PxFileRef?)null);

            // Act
            ActionResult<DataResponse> result = await _controller.GetJsonAsync(database, table, parameters);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<NotFoundResult>());
            _mockLogger.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region PostJsonAsync Tests

        [Test]
        public async Task PostJsonAsync_ValidRequest_ReturnsOkWithDataResponse()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            Dictionary<string, IFilter> query = new() { { "dim0", new CodeFilter(["value1"]) } };            
            DataBaseRef dataBaseRef = DataBaseRef.Create(database);
            PxFileRef fileRef = PxFileRef.Create(table, dataBaseRef);
            IReadOnlyMatrixMetadata mockMetadata = TestMockMetaBuilder.GetMockMetadata();
            DoubleDataValue[] mockData = [
                new DoubleDataValue(1.0, DataValueType.Exists),
                new DoubleDataValue(2.0, DataValueType.Exists)
            ];

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns(dataBaseRef);
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(It.Is<string>(s => s == table), dataBaseRef)).ReturnsAsync(fileRef);
            _cachedDbConnector.Setup(x => x.GetMetadataCachedAsync(It.IsAny<PxFileRef>())).ReturnsAsync(mockMetadata);
            _cachedDbConnector.Setup(x => x.GetDataCachedAsync(It.IsAny<PxFileRef>(), It.IsAny<MatrixMap>())).ReturnsAsync(mockData);

            // Act
            ActionResult<DataResponse> result = await _controller.PostJsonAsync(database, table, query);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
            OkObjectResult? okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            DataResponse? dataResponse = okResult.Value as DataResponse;
            Assert.That(dataResponse, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(dataResponse.Data, Is.SameAs(mockData));
                Assert.That(dataResponse.LastUpdated, Is.EqualTo(new DateTime(2024, 10, 10, 0, 0, 0, DateTimeKind.Utc)));
            });
        }

        [Test]
        public async Task PostJsonAsync_MissingDatabase_ReturnsNotFound()
        {
            // Arrange
            string database = "nonexistent";
            string table = "testtable";
            Dictionary<string, IFilter> query = new() { { "dim0", new CodeFilter(["value1"]) } };

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns((DataBaseRef?)null);

            // Act
            ActionResult<DataResponse> result = await _controller.PostJsonAsync(database, table, query);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<NotFoundResult>());
            _mockLogger.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task PostJsonAsync_MissingTable_ReturnsNotFound()
        {
            // Arrange
            string database = "testdb";
            string table = "nonexistent";
            Dictionary<string, IFilter> query = new() { { "dim0", new CodeFilter(["value1"]) } };

            DataBaseRef dataBaseRef = DataBaseRef.Create(database);
            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns(dataBaseRef);
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(It.Is<string>(s => s == table), dataBaseRef)).ReturnsAsync((PxFileRef?)null);

            // Act
            ActionResult<DataResponse> result = await _controller.PostJsonAsync(database, table, query);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<NotFoundResult>());
            _mockLogger.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region GetJsonStatAsync Tests

        [Test]
        public async Task GetJsonStatAsync_ValidRequest_ReturnsOkWithJsonStat2()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            Dictionary<string, string> parameters = new() { { "dim0.code", "value1" } };
            string lang = "en";
            
            DataBaseRef dataBaseRef = DataBaseRef.Create(database);
            PxFileRef fileRef = PxFileRef.Create(table, dataBaseRef);
            IReadOnlyMatrixMetadata mockMetadata = TestMockMetaBuilder.GetMockMetadata();
            DoubleDataValue[] mockData = [
                new DoubleDataValue(1.0, DataValueType.Exists),
                new DoubleDataValue(2.0, DataValueType.Exists)
            ];

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns(dataBaseRef);
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(It.Is<string>(s => s == table), dataBaseRef)).ReturnsAsync(fileRef);
            _cachedDbConnector.Setup(x => x.GetMetadataCachedAsync(It.IsAny<PxFileRef>())).ReturnsAsync(mockMetadata);
            _cachedDbConnector.Setup(x => x.GetDataCachedAsync(It.IsAny<PxFileRef>(), It.IsAny<MatrixMap>())).ReturnsAsync(mockData);

            // Act
            ActionResult<JsonStat2> result = await _controller.GetJsonStatAsync(database, table, parameters, lang);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
            OkObjectResult? okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            JsonStat2? jsonStat = okResult.Value as JsonStat2;
            Assert.That(jsonStat, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(jsonStat.Version, Is.EqualTo("2.0"));
                Assert.That(jsonStat.Class, Is.EqualTo("dataset"));
                Assert.That(jsonStat.Id, Is.EqualTo("table-tableid"));
                Assert.That(jsonStat.Label, Is.EqualTo("table-description.en"));
                Assert.That(jsonStat.Source, Is.EqualTo("table-source.en"));
                Assert.That(jsonStat.Value, Is.SameAs(mockData));
                
                DateTime expectedLastUpdated = new(2024, 10, 10, 0, 0, 0, DateTimeKind.Utc);
                string expectedUpdated = expectedLastUpdated.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
                Assert.That(jsonStat.Updated, Is.EqualTo(expectedUpdated));
            });
        }

        [Test]
        public async Task GetJsonStatAsync_InvalidLanguage_ReturnsBadRequest()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            Dictionary<string, string> parameters = new() { { "dim0.code", "value1" } };
            string lang = "invalid";
            
            DataBaseRef dataBaseRef = DataBaseRef.Create(database);
            PxFileRef fileRef = PxFileRef.Create(table, dataBaseRef);
            IReadOnlyMatrixMetadata mockMetadata = TestMockMetaBuilder.GetMockMetadata();

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns(dataBaseRef);
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(It.Is<string>(s => s == table), dataBaseRef)).ReturnsAsync(fileRef);
            _cachedDbConnector.Setup(x => x.GetMetadataCachedAsync(It.IsAny<PxFileRef>())).ReturnsAsync(mockMetadata);

            // Act
            ActionResult<JsonStat2> result = await _controller.GetJsonStatAsync(database, table, parameters, lang);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
            BadRequestObjectResult? badRequest = result.Result as BadRequestObjectResult;
            Assert.That(badRequest, Is.Not.Null);
            Assert.That(badRequest.Value, Is.EqualTo("The content is not available in the requested language."));
        }

        [Test]
        public async Task GetJsonStatAsync_FileNotFound_ReturnsNotFound()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            Dictionary<string, string> parameters = new() { { "dim0.code", "value1" } };
            
            DataBaseRef dataBaseRef = DataBaseRef.Create(database);
            PxFileRef fileRef = PxFileRef.Create(table, dataBaseRef);

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns(dataBaseRef);
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(It.Is<string>(s => s == table), dataBaseRef)).ReturnsAsync(fileRef);
            _cachedDbConnector.Setup(ds => ds.GetMetadataCachedAsync(fileRef)).ThrowsAsync(new FileNotFoundException("File not found"));

            // Act
            ActionResult<JsonStat2> result = await _controller.GetJsonStatAsync(database, table, parameters);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<NotFoundObjectResult>());
            NotFoundObjectResult? notFound = result.Result as NotFoundObjectResult;
            Assert.That(notFound, Is.Not.Null);
            Assert.That(notFound.Value, Is.EqualTo("Table or database not found"));
        }

        [Test]
        public async Task GetJsonStatAsync_ArgumentException_ReturnsBadRequest()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            Dictionary<string, string> parameters = new() { { "dim0.code", "value1" } };
            string errorMessage = "Invalid argument";
            
            DataBaseRef dataBaseRef = DataBaseRef.Create(database);
            PxFileRef fileRef = PxFileRef.Create(table, dataBaseRef);

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns(dataBaseRef);
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(It.Is<string>(s => s == table), dataBaseRef)).ReturnsAsync(fileRef);
            _cachedDbConnector.Setup(ds => ds.GetMetadataCachedAsync(fileRef)).ThrowsAsync(new ArgumentException(errorMessage));

            // Act
            ActionResult<JsonStat2> result = await _controller.GetJsonStatAsync(database, table, parameters);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
            BadRequestObjectResult? badRequest = result.Result as BadRequestObjectResult;
            Assert.That(badRequest, Is.Not.Null);
            Assert.That(badRequest.Value, Is.EqualTo(errorMessage));
        }
        
        [Test]
        public async Task GetJsonStatAsync_MissingDatabase_ReturnsNotFound()
        {
            // Arrange
            string database = "nonexistent";
            string table = "testtable";
            Dictionary<string, string> parameters = new() { { "dim0.code", "value1" } };

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns((DataBaseRef?)null);

            // Act
            ActionResult<JsonStat2> result = await _controller.GetJsonStatAsync(database, table, parameters);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<NotFoundResult>());
            _mockLogger.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task GetJsonStatAsync_MissingTable_ReturnsNotFound()
        {
            // Arrange
            string database = "testdb";
            string table = "nonexistent";
            Dictionary<string, string> parameters = new() { { "dim0.code", "value1" } };

            DataBaseRef dataBaseRef = DataBaseRef.Create(database);
            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns(dataBaseRef);
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(It.Is<string>(s => s == table), dataBaseRef)).ReturnsAsync((PxFileRef?)null);

            // Act
            ActionResult<JsonStat2> result = await _controller.GetJsonStatAsync(database, table, parameters);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<NotFoundResult>());
            _mockLogger.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region PostJsonStatAsync Tests

        [Test]
        public async Task PostJsonStatAsync_ValidRequest_ReturnsOkWithJsonStat2()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            Dictionary<string, IFilter> query = new() { { "dim0", new CodeFilter(["value1"]) } };
            string lang = "en";
            
            DataBaseRef dataBaseRef = DataBaseRef.Create(database);
            PxFileRef fileRef = PxFileRef.Create(table, dataBaseRef);
            IReadOnlyMatrixMetadata mockMetadata = TestMockMetaBuilder.GetMockMetadata();
            DoubleDataValue[] mockData = [
                new DoubleDataValue(1.0, DataValueType.Exists),
                new DoubleDataValue(2.0, DataValueType.Exists)
            ];

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns(dataBaseRef);
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(It.Is<string>(s => s == table), dataBaseRef)).ReturnsAsync(fileRef);
            _cachedDbConnector.Setup(x => x.GetMetadataCachedAsync(It.IsAny<PxFileRef>())).ReturnsAsync(mockMetadata);
            _cachedDbConnector.Setup(x => x.GetDataCachedAsync(It.IsAny<PxFileRef>(), It.IsAny<MatrixMap>())).ReturnsAsync(mockData);

            // Act
            ActionResult<JsonStat2> result = await _controller.PostJsonStatAsync(database, table, query, lang);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
            OkObjectResult? okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            JsonStat2? jsonStat = okResult.Value as JsonStat2;
            Assert.That(jsonStat, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(jsonStat.Version, Is.EqualTo("2.0"));
                Assert.That(jsonStat.Class, Is.EqualTo("dataset"));
                Assert.That(jsonStat.Id, Is.EqualTo("table-tableid"));
                Assert.That(jsonStat.Label, Is.EqualTo("table-description.en"));
                Assert.That(jsonStat.Source, Is.EqualTo("table-source.en"));
                Assert.That(jsonStat.Value, Is.SameAs(mockData));
                
                DateTime expectedLastUpdated = new(2024, 10, 10, 0, 0, 0, DateTimeKind.Utc);
                string expectedUpdated = expectedLastUpdated.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
                Assert.That(jsonStat.Updated, Is.EqualTo(expectedUpdated));
            });
        }

        [Test]
        public async Task PostJsonStatAsync_InvalidLanguage_ReturnsBadRequest()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            Dictionary<string, IFilter> query = new() { { "dim0", new CodeFilter(["value1"]) } };
            string lang = "invalid";
            
            DataBaseRef dataBaseRef = DataBaseRef.Create(database);
            PxFileRef fileRef = PxFileRef.Create(table, dataBaseRef);
            IReadOnlyMatrixMetadata mockMetadata = TestMockMetaBuilder.GetMockMetadata();

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns(dataBaseRef);
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(It.Is<string>(s => s == table), dataBaseRef)).ReturnsAsync(fileRef);
            _cachedDbConnector.Setup(ds => ds.GetMetadataCachedAsync(fileRef)).ReturnsAsync(mockMetadata);

            // Act
            ActionResult<JsonStat2> result = await _controller.PostJsonStatAsync(database, table, query, lang);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
            BadRequestObjectResult? badRequest = result.Result as BadRequestObjectResult;
            Assert.That(badRequest, Is.Not.Null);
            Assert.That(badRequest.Value, Is.EqualTo("The content is not available in the requested language."));
        }

        [Test]
        public async Task PostJsonStatAsync_FileNotFound_ReturnsNotFound()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            Dictionary<string, IFilter> query = new() { { "dim0", new CodeFilter(["value1"]) } };
            
            DataBaseRef dataBaseRef = DataBaseRef.Create(database);
            PxFileRef fileRef = PxFileRef.Create(table, dataBaseRef);

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns(dataBaseRef);
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(It.Is<string>(s => s == table), dataBaseRef)).ReturnsAsync(fileRef);
            _cachedDbConnector.Setup(ds => ds.GetMetadataCachedAsync(fileRef)).ThrowsAsync(new FileNotFoundException("File not found"));

            // Act
            ActionResult<JsonStat2> result = await _controller.PostJsonStatAsync(database, table, query);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<NotFoundObjectResult>());
            NotFoundObjectResult? notFound = result.Result as NotFoundObjectResult;
            Assert.That(notFound, Is.Not.Null);
            Assert.That(notFound.Value, Is.EqualTo("Table or database not found"));
        }

        [Test]
        public async Task PostJsonStatAsync_ArgumentException_ReturnsBadRequest()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            Dictionary<string, IFilter> query = new() { { "dim0", new CodeFilter(["value1"]) } };
            string errorMessage = "Invalid argument";
            
            DataBaseRef dataBaseRef = DataBaseRef.Create(database);
            PxFileRef fileRef = PxFileRef.Create(table, dataBaseRef);

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns(dataBaseRef);
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(It.Is<string>(s => s == table), dataBaseRef)).ReturnsAsync(fileRef);
            _cachedDbConnector.Setup(ds => ds.GetMetadataCachedAsync(fileRef)).ThrowsAsync(new ArgumentException(errorMessage));

            // Act
            ActionResult<JsonStat2> result = await _controller.PostJsonStatAsync(database, table, query);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
            BadRequestObjectResult? badRequest = result.Result as BadRequestObjectResult;
            Assert.That(badRequest, Is.Not.Null);
            Assert.That(badRequest.Value, Is.EqualTo(errorMessage));
        }

        [Test]
        public async Task PostJsonStatAsync_MissingDatabase_ReturnsNotFound()
        {
            // Arrange
            string database = "nonexistent";
            string table = "testtable";
            Dictionary<string, IFilter> query = new() { { "dim0", new CodeFilter(["value1"]) } };

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns((DataBaseRef?)null);

            // Act
            ActionResult<JsonStat2> result = await _controller.PostJsonStatAsync(database, table, query);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<NotFoundResult>());
            _mockLogger.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task PostJsonStatAsync_MissingTable_ReturnsNotFound()
        {
            // Arrange
            string database = "testdb";
            string table = "nonexistent";
            Dictionary<string, IFilter> query = new() { { "dim0", new CodeFilter(["value1"]) } };

            DataBaseRef dataBaseRef = DataBaseRef.Create(database);
            _cachedDbConnector.Setup(x => x.GetDataBaseReference(It.Is<string>(s => s == database))).Returns(dataBaseRef);
            _cachedDbConnector.Setup(x => x.GetFileReferenceCachedAsync(It.Is<string>(s => s == table), dataBaseRef)).ReturnsAsync((PxFileRef?)null);

            // Act
            ActionResult<JsonStat2> result = await _controller.PostJsonStatAsync(database, table, query);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<NotFoundResult>());
            _mockLogger.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion
    }
}