using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Px.Utils.Models.Metadata;
using PxApi.Caching;
using PxApi.Configuration;
using PxApi.Controllers;
using PxApi.Models;
using PxApi.Models.JsonStat;
using PxApi.UnitTests.ModelBuilderTests;
using PxApi.UnitTests.Utils;

namespace PxApi.UnitTests.ControllerTests
{
    [TestFixture]
    internal class MetadataControllerTest
    {
        private Mock<ICachedDataSource> _mockDbConnector;
        private MetadataController _controller;

        [SetUp]
        public void SetUp()
        {
            _mockDbConnector = new Mock<ICachedDataSource>();
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
                {"DataBases:0:Custom:FileListingCacheDurationMs", "10000"}

            };

            IConfiguration _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            AppSettings.Load(_configuration);
        }

        [Test]
        public async Task GetMetadataById_FileExists_ReturnsJsonStat2()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("exampledb");
            PxFileRef file = PxFileRef.CreateFromPath(Path.Combine("c:", "testfolder", "filename.px"), database);
            string lang = "en";
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();
            List<TableGroup> groups = [TableGroupTestUtils.CreateTestTableGroup()];

            _mockDbConnector.Setup(x => x.GetDataBaseReference(database.Id)).Returns(database);
            _mockDbConnector.Setup(x => x.GetFileReferenceCachedAsync(file.Id, database)).ReturnsAsync(file);
            _mockDbConnector.Setup(x => x.GetMetadataCachedAsync(file)).ReturnsAsync(meta);
            _mockDbConnector.Setup(x => x.GetGroupingsCachedAsync(file)).ReturnsAsync(groups);

            // Act
            ActionResult<JsonStat2> result = await _controller.GetTableMetadataById(database.Id, file.Id, lang);

            // Assert
            Assert.That(result, Is.InstanceOf<ActionResult<JsonStat2>>());
            OkObjectResult? okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            JsonStat2? resultMeta = okResult.Value as JsonStat2;
            Assert.That(resultMeta, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(resultMeta.Id, Is.EqualTo(new string[] { "dim1", "dim2", "time", "content" }));
                Assert.That(resultMeta.Label, Is.EqualTo("Test table description"));
                Assert.That(resultMeta.Source, Is.EqualTo("Test source"));
                Assert.That(resultMeta.Dimension, Has.Count.EqualTo(4));
                Assert.That(resultMeta.Size, Has.Count.EqualTo(4));
            });
        }

        [Test]
        public async Task GetMetadataById_FileDoesNotExist_ReturnsNotFound()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("exampledb");
            PxFileRef file = PxFileRef.CreateFromPath(Path.Combine("c:", "testfolder", "filename.px"), database);

            _mockDbConnector.Setup(ds => ds.GetFileReferenceCachedAsync(file.Id, database)).ThrowsAsync(new FileNotFoundException());

            // Act
            ActionResult<JsonStat2> result = await _controller.GetTableMetadataById(database.Id, file.Id, null);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<NotFoundResult>());
        }

        [Test]
        public async Task GetMetadataById_LanguageNotAvailable_ReturnsBadRequest()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("exampledb");
            PxFileRef file = PxFileRef.CreateFromPath(Path.Combine("c:", "testfolder", "filename.px"), database);
            string lang = "de";
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();

            _mockDbConnector.Setup(x => x.GetDataBaseReference(database.Id)).Returns(database);
            _mockDbConnector.Setup(x => x.GetFileReferenceCachedAsync(file.Id, database)).ReturnsAsync(file);
            _mockDbConnector.Setup(x => x.GetMetadataCachedAsync(file)).ReturnsAsync(meta);

            // Act
            ActionResult<JsonStat2> result = await _controller.GetTableMetadataById(database.Id, file.Id, lang);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
            BadRequestObjectResult? badRequestResult = result.Result as BadRequestObjectResult;
            Assert.That(badRequestResult?.Value, Is.EqualTo("The content is not available in the requested language."));
        }

        [Test]
        public async Task GetMetadataById_NoLanguageSpecified_ReturnsJsonStat2()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("exampledb");
            PxFileRef file = PxFileRef.CreateFromPath(Path.Combine("c:", "testfolder", "filename.px"), database);
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();
            List<TableGroup> groups = [TableGroupTestUtils.CreateTestTableGroup()];

            _mockDbConnector.Setup(x => x.GetDataBaseReference(database.Id)).Returns(database);
            _mockDbConnector.Setup(ds => ds.GetFileReferenceCachedAsync(file.Id, database)).ReturnsAsync(file);
            _mockDbConnector.Setup(ds => ds.GetMetadataCachedAsync(file)).ReturnsAsync(meta);
            _mockDbConnector.Setup(x => x.GetGroupingsCachedAsync(file)).ReturnsAsync(groups);

            // Act
            ActionResult<JsonStat2> result = await _controller.GetTableMetadataById(database.Id, file.Id, null);

            // Assert
            Assert.That(result, Is.InstanceOf<ActionResult<JsonStat2>>()); 
            OkObjectResult? okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            JsonStat2? resultMeta = okResult.Value as JsonStat2;
            Assert.That(resultMeta, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(resultMeta.Id, Is.EqualTo(new string[] { "dim1", "dim2", "time", "content" }));
                Assert.That(resultMeta.Label, Is.EqualTo("Test table description"));
                Assert.That(resultMeta.Source, Is.EqualTo("Test source"));
                Assert.That(resultMeta.Dimension, Has.Count.EqualTo(4));
                Assert.That(resultMeta.Size, Has.Count.EqualTo(4));
            });
        }
    }
}
