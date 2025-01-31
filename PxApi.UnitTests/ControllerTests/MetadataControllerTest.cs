using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Px.Utils.Models.Metadata;
using PxApi.Configuration;
using PxApi.Controllers;
using PxApi.DataSources;
using PxApi.Models;
using PxApi.UnitTests.ModelBuilderTests;

namespace PxApi.UnitTests.ControllerTests
{
    [TestFixture]
    internal class MetadataControllerTest
    {
        private Mock<IDataSource> _mockDataSource;
        private MetadataController _controller;

        [SetUp]
        public void SetUp()
        {
            _mockDataSource = new Mock<IDataSource>();
            _controller = new MetadataController(_mockDataSource.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };

            Dictionary<string, string?> inMemorySettings = new()
            {
                {"RootUrl", "https://testurl.fi"},
                {"DataSource:LocalFileSystem:RootPath", "datasource/root/"}
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
            string database = "example-db";
            string file = "filename.px";
            string lang = "en";
            TablePath tablePath = new($"datasource/root/{database}/{file}");
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();

            _mockDataSource.Setup(ds => ds.GetTablePathAsync(database, file)).ReturnsAsync(tablePath);
            _mockDataSource.Setup(ds => ds.GetTableMetadataAsync(tablePath)).ReturnsAsync(meta);

            // Act
            ActionResult<TableMeta> result = await _controller.GetTableMetadataById(database, file, lang, true);

            // Assert
            Assert.That(result, Is.InstanceOf<ActionResult<TableMeta>>());
            OkObjectResult? okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            TableMeta? resultMeta = okResult.Value as TableMeta;
            Assert.That(resultMeta, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(resultMeta.Links[0].Href, Is.EqualTo("https://testurl.fi/meta/example-db/filename?lang=en&showValues=true"));
                Assert.That(resultMeta.Links[0].Rel, Is.EqualTo("self"));
                Assert.That(resultMeta.Links[0].Method, Is.EqualTo("GET"));
            });
        }

        [Test]
        public async Task GetMetadataById_FileDoesNotExist_ReturnsNotFound()
        {
            // Arrange
            string database = "example-db";
            string file = "filename.px";

            _mockDataSource.Setup(ds => ds.GetTablePathAsync(database, file)).ReturnsAsync((TablePath?)null);

            // Act
            ActionResult<TableMeta> result = await _controller.GetTableMetadataById(database, file, null, true);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<NotFoundResult>());
        }

        [Test]
        public async Task GetMetadataById_LanguageNotAvailable_ReturnsBadRequest()
        {
            // Arrange
            string database = "example-db";
            string file = "filename.px";
            string lang = "de";
            TablePath tablePath = new($"datasource/root/{database}/{file}");
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();

            _mockDataSource.Setup(ds => ds.GetTablePathAsync(database, file)).ReturnsAsync(tablePath);
            _mockDataSource.Setup(ds => ds.GetTableMetadataAsync(tablePath)).ReturnsAsync(meta);

            // Act
            ActionResult<TableMeta> result = await _controller.GetTableMetadataById(database, file, lang, true);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
            BadRequestObjectResult? badRequestResult = result.Result as BadRequestObjectResult;
            Assert.That(badRequestResult?.Value, Is.EqualTo($"The content is not available in language: {lang}"));
        }

        [Test]
        public async Task GetMetadataById_NoLanguageSpecified_ReturnsTableMeta()
        {
            // Arrange
            string database = "example-db";
            string file = "filename.px";
            TablePath tablePath = new($"datasource/root/{database}/{file}");
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();

            _mockDataSource.Setup(ds => ds.GetTablePathAsync(database, file)).ReturnsAsync(tablePath);
            _mockDataSource.Setup(ds => ds.GetTableMetadataAsync(tablePath)).ReturnsAsync(meta);

            // Act
            ActionResult<TableMeta> result = await _controller.GetTableMetadataById(database, file, null, null);

            // Assert
            Assert.That(result, Is.InstanceOf<ActionResult<TableMeta>>()); 
            OkObjectResult? okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            TableMeta? resultMeta = okResult.Value as TableMeta;
            Assert.That(resultMeta, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(resultMeta.Links[0].Href, Is.EqualTo("https://testurl.fi/meta/example-db/filename"));
                Assert.That(resultMeta.Links[0].Rel, Is.EqualTo("self"));
                Assert.That(resultMeta.Links[0].Method, Is.EqualTo("GET"));
            });
        }

        [Test]
        public async Task GetVariableMeta_ContentVariableExists_ReturnsVariableMeta()
        {
            // Arrange
            string database = "example-db";
            string file = "filename.px";
            string lang = "en";
            string varcode = "content-code";
            TablePath tablePath = new($"datasource/root/{database}/{file}");
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();

            _mockDataSource.Setup(ds => ds.GetTablePathAsync(database, file)).ReturnsAsync(tablePath);
            _mockDataSource.Setup(ds => ds.GetTableMetadataAsync(tablePath)).ReturnsAsync(meta);

            // Act
            ActionResult<VariableBase> result = await _controller.GetVariableMeta(database, file, varcode, lang);

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
            string database = "example-db";
            string file = "filename.px";
            string lang = "en";
            string varcode = "time-code";
            TablePath tablePath = new($"datasource/root/{database}/{file}");
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();

            _mockDataSource.Setup(ds => ds.GetTablePathAsync(database, file)).ReturnsAsync(tablePath);
            _mockDataSource.Setup(ds => ds.GetTableMetadataAsync(tablePath)).ReturnsAsync(meta);

            // Act
            ActionResult<VariableBase> result = await _controller.GetVariableMeta(database, file, varcode, lang);

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
            string database = "example-db";
            string file = "filename.px";
            string varcode = "nonexistent-varcode";
            TablePath tablePath = new($"datasource/root/{database}/{file}");
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();

            _mockDataSource.Setup(ds => ds.GetTablePathAsync(database, file)).ReturnsAsync(tablePath);
            _mockDataSource.Setup(ds => ds.GetTableMetadataAsync(tablePath)).ReturnsAsync(meta);

            // Act
            ActionResult<VariableBase> result = await _controller.GetVariableMeta(database, file, varcode, null);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<NotFoundResult>());
        }

        [Test]
        public async Task GetVariableMeta_LanguageNotAvailable_ReturnsNotFound()
        {
            // Arrange
            string database = "example-db";
            string file = "filename.px";
            string varcode = "varcode";
            string lang = "de";
            TablePath tablePath = new($"datasource/root/{database}/{file}");
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();

            _mockDataSource.Setup(ds => ds.GetTablePathAsync(database, file)).ReturnsAsync(tablePath);
            _mockDataSource.Setup(ds => ds.GetTableMetadataAsync(tablePath)).ReturnsAsync(meta);

            // Act
            ActionResult<VariableBase> result = await _controller.GetVariableMeta(database, file, varcode, lang);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<NotFoundResult>());
            NotFoundResult? notFoundResult = result.Result as NotFoundResult;
            Assert.That(notFoundResult, Is.Not.Null);
        }

        [Test]
        public async Task GetVariableMeta_NoLanguageSpecified_ReturnsVariableMeta()
        {
            // Arrange
            string database = "example-db";
            string file = "filename.px";
            string varcode = "dim0-code";
            TablePath tablePath = new($"datasource/root/{database}/{file}");
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();

            _mockDataSource.Setup(ds => ds.GetTablePathAsync(database, file)).ReturnsAsync(tablePath);
            _mockDataSource.Setup(ds => ds.GetTableMetadataAsync(tablePath)).ReturnsAsync(meta);

            // Act
            ActionResult<VariableBase> result = await _controller.GetVariableMeta(database, file, varcode, null);

            // Assert
            Assert.That(result, Is.InstanceOf<ActionResult<VariableBase>>());
            OkObjectResult? okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            Variable? resultMeta = okResult.Value as Variable;
            Assert.That(resultMeta, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(resultMeta.Links[0].Href, Is.EqualTo("https://testurl.fi/meta/example-db/filename/dim0-code"));
                Assert.That(resultMeta.Links[0].Rel, Is.EqualTo("self"));
                Assert.That(resultMeta.Links[0].Method, Is.EqualTo("GET"));
                Assert.That(resultMeta.Links[1].Href, Is.EqualTo("https://testurl.fi/meta/example-db/filename"));
                Assert.That(resultMeta.Links[1].Rel, Is.EqualTo("up"));
                Assert.That(resultMeta.Links[1].Method, Is.EqualTo("GET"));
            });
        }
    }
}
