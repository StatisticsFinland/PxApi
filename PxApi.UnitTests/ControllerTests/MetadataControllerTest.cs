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
            ActionResult<TableMeta> result = await _controller.GetMetadataById(database, file, lang, true);

            // Assert
            Assert.That(result, Is.InstanceOf<ActionResult<TableMeta>>());
            OkObjectResult? okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            Assert.That(okResult.Value, Is.InstanceOf<TableMeta>());
        }

        [Test]
        public async Task GetMetadataById_FileDoesNotExist_ReturnsNotFound()
        {
            // Arrange
            string database = "example-db";
            string file = "filename.px";

            _mockDataSource.Setup(ds => ds.GetTablePathAsync(database, file)).ReturnsAsync((TablePath?)null);

            // Act
            ActionResult<TableMeta> result = await _controller.GetMetadataById(database, file, null, true);

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
            ActionResult<TableMeta> result = await _controller.GetMetadataById(database, file, lang, true);

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
            ActionResult<TableMeta> result = await _controller.GetMetadataById(database, file, null, null);

            // Assert
            Assert.That(result, Is.InstanceOf<ActionResult<TableMeta>>());
            OkObjectResult? okResult = result.Result as OkObjectResult;

            Assert.That(okResult, Is.Not.Null);
            Assert.That(okResult.Value, Is.InstanceOf<TableMeta>());
        }
    }
}
