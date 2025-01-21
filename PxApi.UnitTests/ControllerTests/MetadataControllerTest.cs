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
                {"DataSource:LocalFileSystem:RootPath", "path/to/datasource"}
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
            string path = "some/path";
            string lang = "en";
            List<string> hierarchy = ["some", "path"];
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();

            _mockDataSource.Setup(ds => ds.IsFileAsync(hierarchy)).ReturnsAsync(true);
            _mockDataSource.Setup(ds => ds.GetTableMetadataAsync(hierarchy)).ReturnsAsync(meta);

            // Act
            ActionResult<TableMeta> result = await _controller.GetMetadataById(path, lang);

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
            string path = "some/path";
            List<string> hierarchy = ["some", "path"];

            _mockDataSource.Setup(ds => ds.IsFileAsync(hierarchy)).ReturnsAsync(false);

            // Act
            ActionResult<TableMeta> result = await _controller.GetMetadataById(path, null);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<NotFoundResult>());
        }

        [Test]
        public async Task GetMetadataById_LanguageNotAvailable_ReturnsBadRequest()
        {
            // Arrange
            string path = "some/path";
            string lang = "de";
            List<string> hierarchy = ["some", "path"];
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();

            _mockDataSource.Setup(ds => ds.IsFileAsync(hierarchy)).ReturnsAsync(true);
            _mockDataSource.Setup(ds => ds.GetTableMetadataAsync(hierarchy)).ReturnsAsync(meta);

            // Act
            ActionResult<TableMeta> result = await _controller.GetMetadataById(path, lang);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
            BadRequestObjectResult? badRequestResult = result.Result as BadRequestObjectResult;
            Assert.That(badRequestResult?.Value, Is.EqualTo($"The content is not available in language: {lang}"));
        }

        [Test]
        public async Task GetMetadataById_NoLanguageSpecified_ReturnsTableMeta()
        {
            // Arrange
            string path = "some/path";
            List<string> hierarchy = ["some", "path"];
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();

            _mockDataSource.Setup(ds => ds.IsFileAsync(hierarchy)).ReturnsAsync(true);
            _mockDataSource.Setup(ds => ds.GetTableMetadataAsync(hierarchy)).ReturnsAsync(meta);

            // Act
            ActionResult<TableMeta> result = await _controller.GetMetadataById(path, null);

            // Assert
            Assert.That(result, Is.InstanceOf<ActionResult<TableMeta>>());
            OkObjectResult? okResult = result.Result as OkObjectResult;

            Assert.That(okResult, Is.Not.Null);
            Assert.That(okResult.Value, Is.InstanceOf<TableMeta>());
        }
    }
}
