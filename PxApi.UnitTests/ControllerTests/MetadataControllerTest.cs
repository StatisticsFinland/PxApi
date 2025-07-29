using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Px.Utils.Models.Metadata;
using PxApi.Caching;
using PxApi.Configuration;
using PxApi.Controllers;
using PxApi.Models;
using PxApi.UnitTests.ModelBuilderTests;

namespace PxApi.UnitTests.ControllerTests
{
    [TestFixture]
    internal class MetadataControllerTest
    {
        private Mock<ICachedDataBaseConnector> _mockDbConnector;
        private MetadataController _controller;

        [SetUp]
        public void SetUp()
        {
            _mockDbConnector = new Mock<ICachedDataBaseConnector>();
            _controller = new MetadataController(_mockDbConnector.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };

            Dictionary<string, string?> inMemorySettings = new()
            {
                {"RootUrl", "https://testurl.fi"},
                {"DataSource:LocalFileSystem:RootPath", "datasource/root/"},
                {"DataSource:LocalFileSystem:MetadataCache:SlidingExpirationMinutes", "15"},
                {"DataSource:LocalFileSystem:MetadataCache:AbsoluteExpirationMinutes", "15"},
                {"DataSource:LocalFileSystem:ModifiedCheckIntervalMs", "1000"},
                {"DataSource:LocalFileSystem:FileListingCacheDurationMs", "10000"},
                {"DataSource:LocalFileSystem:DataCache:SlidingExpirationMinutes", "10"},
                {"DataSource:LocalFileSystem:DataCache:AbsoluteExpirationMinutes", "10" }
            };

            IConfiguration _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            AppSettings.Load(_configuration);
        }

        [Test]
        public async Task GetMetadataById_FileExists_ReturnsTableMeta()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("exampledb");
            PxFileRef file = PxFileRef.Create("filename", database);
            string lang = "en";
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();

            _mockDbConnector.Setup(x => x.GetDataBaseReference(database.Id)).Returns(database);
            _mockDbConnector.Setup(x => x.GetFileReferenceCachedAsync(file.Id, database)).ReturnsAsync(file);
            _mockDbConnector.Setup(x => x.GetMetadataCachedAsync(file)).ReturnsAsync(meta);

            // Act
            ActionResult<TableMeta> result = await _controller.GetTableMetadataById(database.Id, file.Id, lang, true);

            // Assert
            Assert.That(result, Is.InstanceOf<ActionResult<TableMeta>>());
            OkObjectResult? okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            TableMeta? resultMeta = okResult.Value as TableMeta;
            Assert.That(resultMeta, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(resultMeta.Links[0].Href, Is.EqualTo("https://testurl.fi/meta/exampledb/filename?lang=en&showValues=true"));
                Assert.That(resultMeta.Links[0].Rel, Is.EqualTo("self"));
                Assert.That(resultMeta.Links[0].Method, Is.EqualTo("GET"));
            });
        }

        [Test]
        public async Task GetMetadataById_FileDoesNotExist_ReturnsNotFound()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("exampledb");
            PxFileRef file = PxFileRef.Create("filename", database);

            _mockDbConnector.Setup(ds => ds.GetFileReferenceCachedAsync(file.Id, database)).ThrowsAsync(new FileNotFoundException());

            // Act
            ActionResult<TableMeta> result = await _controller.GetTableMetadataById(database.Id, file.Id, null, true);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<NotFoundResult>());
        }

        [Test]
        public async Task GetMetadataById_LanguageNotAvailable_ReturnsBadRequest()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("exampledb");
            PxFileRef file = PxFileRef.Create("filename", database);
            string lang = "de";
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();

            _mockDbConnector.Setup(x => x.GetDataBaseReference(database.Id)).Returns(database);
            _mockDbConnector.Setup(x => x.GetFileReferenceCachedAsync(file.Id, database)).ReturnsAsync(file);
            _mockDbConnector.Setup(x => x.GetMetadataCachedAsync(file)).ReturnsAsync(meta);

            // Act
            ActionResult<TableMeta> result = await _controller.GetTableMetadataById(database.Id, file.Id, lang, true);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
            BadRequestObjectResult? badRequestResult = result.Result as BadRequestObjectResult;
            Assert.That(badRequestResult?.Value, Is.EqualTo("The content is not available in the requested language."));
        }

        [Test]
        public async Task GetMetadataById_NoLanguageSpecified_ReturnsTableMeta()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("exampledb");
            PxFileRef file = PxFileRef.Create("filename", database);
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();

            _mockDbConnector.Setup(x => x.GetDataBaseReference(database.Id)).Returns(database);
            _mockDbConnector.Setup(ds => ds.GetFileReferenceCachedAsync(file.Id, database)).ReturnsAsync(file);
            _mockDbConnector.Setup(ds => ds.GetMetadataCachedAsync(file)).ReturnsAsync(meta);

            // Act
            ActionResult<TableMeta> result = await _controller.GetTableMetadataById(database.Id, file.Id, null, null);

            // Assert
            Assert.That(result, Is.InstanceOf<ActionResult<TableMeta>>()); 
            OkObjectResult? okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            TableMeta? resultMeta = okResult.Value as TableMeta;
            Assert.That(resultMeta, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(resultMeta.Links[0].Href, Is.EqualTo("https://testurl.fi/meta/exampledb/filename"));
                Assert.That(resultMeta.Links[0].Rel, Is.EqualTo("self"));
                Assert.That(resultMeta.Links[0].Method, Is.EqualTo("GET"));
            });
        }

        [Test]
        public async Task GetVariableMeta_ContentVariableExists_ReturnsVariableMeta()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("exampledb");
            PxFileRef file = PxFileRef.Create("filename", database);
            string lang = "en";
            string varcode = "content-code";
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();

            _mockDbConnector.Setup(x => x.GetDataBaseReference(database.Id)).Returns(database);
            _mockDbConnector.Setup(ds => ds.GetFileReferenceCachedAsync(file.Id, database)).ReturnsAsync(file);
            _mockDbConnector.Setup(ds => ds.GetMetadataCachedAsync(file)).ReturnsAsync(meta);

            // Act
            ActionResult<VariableBase> result = await _controller.GetVariableMeta(database.Id, file.Id, varcode, lang);

            // Assert
            Assert.That(result, Is.InstanceOf<ActionResult<VariableBase>>());
            OkObjectResult? okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            ContentVariable? contentVar = okResult.Value as ContentVariable;
            Assert.That(contentVar, Is.Not.Null);
        }

        [Test]
        public async Task GetVariableMeta_TimeVariableExists_ReturnsVariableMeta()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("exampleb");
            PxFileRef file = PxFileRef.Create("filename", database);
            string lang = "en";
            string varcode = "time-code";
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();

            _mockDbConnector.Setup(x => x.GetDataBaseReference(database.Id)).Returns(database);
            _mockDbConnector.Setup(ds => ds.GetFileReferenceCachedAsync(file.Id, database)).ReturnsAsync(file);
            _mockDbConnector.Setup(ds => ds.GetMetadataCachedAsync(file)).ReturnsAsync(meta);

            // Act
            ActionResult<VariableBase> result = await _controller.GetVariableMeta(database.Id, file.Id, varcode, lang);

            // Assert
            Assert.That(result, Is.InstanceOf<ActionResult<VariableBase>>());
            OkObjectResult? okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            TimeVariable? timeVar = okResult.Value as TimeVariable;
            Assert.That(timeVar, Is.Not.Null);
        }

        [Test]
        public async Task GetVariableMeta_VariableDoesNotExist_ReturnsNotFound()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("exampledb");
            PxFileRef file = PxFileRef.Create("filename", database);
            string varcode = "nonexistent-varcode";
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();

            _mockDbConnector.Setup(x => x.GetDataBaseReference(database.Id)).Returns(database);
            _mockDbConnector.Setup(ds => ds.GetFileReferenceCachedAsync(file.Id, database)).ReturnsAsync(file);
            _mockDbConnector.Setup(ds => ds.GetMetadataCachedAsync(file)).ReturnsAsync(meta);

            // Act
            ActionResult<VariableBase> result = await _controller.GetVariableMeta(database.Id, file.Id, varcode, null);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<NotFoundResult>());
        }

        [Test]
        public async Task GetVariableMeta_LanguageNotAvailable_ReturnsNotFound()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("exampledb");
            PxFileRef file = PxFileRef.Create("filename", database);
            string varcode = "varcode";
            string lang = "de";
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();

            _mockDbConnector.Setup(x => x.GetDataBaseReference(database.Id)).Returns(database);
            _mockDbConnector.Setup(ds => ds.GetFileReferenceCachedAsync(file.Id, database)).ReturnsAsync(file);
            _mockDbConnector.Setup(ds => ds.GetMetadataCachedAsync(file)).ReturnsAsync(meta);

            // Act
            ActionResult<VariableBase> result = await _controller.GetVariableMeta(database.Id, file.Id, varcode, lang);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<NotFoundResult>());
            NotFoundResult? notFoundResult = result.Result as NotFoundResult;
            Assert.That(notFoundResult, Is.Not.Null);
        }

        [Test]
        public async Task GetVariableMeta_NoLanguageSpecified_ReturnsVariableMeta()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("exampledb");
            PxFileRef file = PxFileRef.Create("filename", database);
            string varcode = "dim0-code";
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();

            _mockDbConnector.Setup(x => x.GetDataBaseReference(database.Id)).Returns(database);
            _mockDbConnector.Setup(ds => ds.GetFileReferenceCachedAsync(file.Id, database)).ReturnsAsync(file);
            _mockDbConnector.Setup(ds => ds.GetMetadataCachedAsync(file)).ReturnsAsync(meta);

            // Act
            ActionResult<VariableBase> result = await _controller.GetVariableMeta(database.Id, file.Id, varcode, null);

            // Assert
            Assert.That(result, Is.InstanceOf<ActionResult<VariableBase>>());
            OkObjectResult? okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            Variable? resultMeta = okResult.Value as Variable;
            Assert.That(resultMeta, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(resultMeta.Links[0].Href, Is.EqualTo("https://testurl.fi/meta/exampledb/filename/dim0-code"));
                Assert.That(resultMeta.Links[0].Rel, Is.EqualTo("self"));
                Assert.That(resultMeta.Links[0].Method, Is.EqualTo("GET"));
                Assert.That(resultMeta.Links[1].Href, Is.EqualTo("https://testurl.fi/meta/exampledb/filename"));
                Assert.That(resultMeta.Links[1].Rel, Is.EqualTo("up"));
                Assert.That(resultMeta.Links[1].Method, Is.EqualTo("GET"));
            });
        }
    }
}
