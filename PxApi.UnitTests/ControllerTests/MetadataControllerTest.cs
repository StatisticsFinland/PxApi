using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Px.Utils.Models.Metadata;
using PxApi.Caching;
using PxApi.Controllers;
using PxApi.Models.JsonStat;
using PxApi.Models;
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

            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                TestConfigFactory.MountedDb(0, "testdb", "datasource/root/"),
                new Dictionary<string, string?>
                {
                    ["DataBases:0:CacheConfig:Modifiedtime:SlidingExpirationSeconds"] = "60",
                    ["DataBases:0:CacheConfig:Modifiedtime:AbsoluteExpirationSeconds"] = "60",
                    ["DataBases:0:CacheConfig:MaxCacheSize"] = "1073741824",
                    ["DataBases:0:Custom:ModifiedCheckIntervalMs"] = "1000",
                    ["DataBases:0:Custom:FileListingCacheDurationMs"] = "10000"
                }
            );
            TestConfigFactory.BuildAndLoad(configData);
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
            string[] expectedId = ["content-code", "time-code", "dim0-code", "dim1-code"];

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
                Assert.That(resultMeta.Id, Is.EqualTo(expectedId));
                Assert.That(resultMeta.Label, Is.EqualTo("table-description.en"));
                Assert.That(resultMeta.Source, Is.EqualTo("table-source.en"));
                Assert.That(resultMeta.Dimension, Has.Count.EqualTo(4));
                Assert.That(resultMeta.Size, Has.Count.EqualTo(4));
            });
        }

        [Test]
        public async Task GetMetadataById_WithMultipleGroupings_ReturnsJsonStat2WithAllGroupings()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("exampledb");
            PxFileRef file = PxFileRef.CreateFromPath(Path.Combine("c:", "testfolder", "filename.px"), database);
            string lang = "fi";
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();
            List<TableGroup> groups = TableGroupTestUtils.CreateTestTableGroups(3);
            string[] expectedId = ["content-code", "time-code", "dim0-code", "dim1-code"];

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
                Assert.That(resultMeta.Id, Is.EqualTo(expectedId));
                Assert.That(resultMeta.Label, Is.EqualTo("table-description.fi")); // Using default language
                Assert.That(resultMeta.Source, Is.EqualTo("table-source.fi"));
                Assert.That(resultMeta.Dimension, Has.Count.EqualTo(4));
                Assert.That(resultMeta.Size, Has.Count.EqualTo(4));
                Assert.That(resultMeta.Extension, Is.Not.Null);
                Assert.That(resultMeta.Extension!.ContainsKey("groupings"));
                Assert.That(resultMeta.Extension["groupings"], Is.InstanceOf<List<TableGroupJsonStatExtension>>());
                Assert.That(resultMeta.Extension["groupings"] as List<TableGroupJsonStatExtension>, Has.Count.EqualTo(3));
            });

            // Verify that GetGroupingsCachedAsync was called with the correct file
            _mockDbConnector.Verify(x => x.GetGroupingsCachedAsync(file), Times.Once);
        }

        [Test]
        public async Task GetMetadataById_WithEmptyGroupings_ReturnsJsonStat2()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("exampledb");
            PxFileRef file = PxFileRef.CreateFromPath(Path.Combine("c:", "testfolder", "filename.px"), database);
            string lang = "sv";
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();
            List<TableGroup> emptyGroups = []; // Empty groupings list
            string[] expectedId = ["content-code", "time-code", "dim0-code", "dim1-code"];

            _mockDbConnector.Setup(x => x.GetDataBaseReference(database.Id)).Returns(database);
            _mockDbConnector.Setup(x => x.GetFileReferenceCachedAsync(file.Id, database)).ReturnsAsync(file);
            _mockDbConnector.Setup(x => x.GetMetadataCachedAsync(file)).ReturnsAsync(meta);
            _mockDbConnector.Setup(x => x.GetGroupingsCachedAsync(file)).ReturnsAsync(emptyGroups);

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
                Assert.That(resultMeta.Id, Is.EqualTo(expectedId));
                Assert.That(resultMeta.Label, Is.EqualTo("table-description.sv"));
                Assert.That(resultMeta.Source, Is.EqualTo("table-source.sv"));
                Assert.That(resultMeta.Dimension, Has.Count.EqualTo(4));
                Assert.That(resultMeta.Size, Has.Count.EqualTo(4));
                Assert.That(resultMeta.Extension!.ContainsKey("groupings"));
                Assert.That(resultMeta.Extension["groupings"], Is.InstanceOf<List<TableGroupJsonStatExtension>>());
                Assert.That(resultMeta.Extension["groupings"] as List<TableGroupJsonStatExtension>, Has.Count.EqualTo(0));
            });

            // Verify that GetGroupingsCachedAsync was called even when returning empty list
            _mockDbConnector.Verify(x => x.GetGroupingsCachedAsync(file), Times.Once);
        }

        [Test]
        public async Task GetMetadataById_GroupingsCacheThrowsException_ReturnsNotFound()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("exampledb");
            PxFileRef file = PxFileRef.CreateFromPath(Path.Combine("c:", "testfolder", "filename.px"), database);
            string lang = "en";
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();

            _mockDbConnector.Setup(x => x.GetDataBaseReference(database.Id)).Returns(database);
            _mockDbConnector.Setup(x => x.GetFileReferenceCachedAsync(file.Id, database)).ReturnsAsync(file);
            _mockDbConnector.Setup(x => x.GetMetadataCachedAsync(file)).ReturnsAsync(meta);
            _mockDbConnector.Setup(x => x.GetGroupingsCachedAsync(file)).ThrowsAsync(new FileNotFoundException("Grouping file not found"));

            // Act
            ActionResult<JsonStat2> result = await _controller.GetTableMetadataById(database.Id, file.Id, lang);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<NotFoundObjectResult>());
            NotFoundObjectResult? notFoundResult = result.Result as NotFoundObjectResult;
            Assert.That(notFoundResult?.Value, Is.EqualTo("Resource not found."));
        }

        [Test]
        public async Task GetMetadataById_GroupingsCacheThrowsGeneralException_ReturnsInternalServerError()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("exampledb");
            PxFileRef file = PxFileRef.CreateFromPath(Path.Combine("c:", "testfolder", "filename.px"), database);
            string lang = "en";
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();

            _mockDbConnector.Setup(x => x.GetDataBaseReference(database.Id)).Returns(database);
            _mockDbConnector.Setup(x => x.GetFileReferenceCachedAsync(file.Id, database)).ReturnsAsync(file);
            _mockDbConnector.Setup(x => x.GetMetadataCachedAsync(file)).ReturnsAsync(meta);
            _mockDbConnector.Setup(x => x.GetGroupingsCachedAsync(file)).ThrowsAsync(new InvalidOperationException("Cache error"));

            // Act
            ActionResult<JsonStat2> result = await _controller.GetTableMetadataById(database.Id, file.Id, lang);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<ObjectResult>());
            ObjectResult? objectResult = result.Result as ObjectResult;
            Assert.Multiple(() =>
            {
                Assert.That(objectResult?.StatusCode, Is.EqualTo(500));
                Assert.That(objectResult?.Value, Is.EqualTo("Unexpected server error."));
            });
        }

        [Test]
        public async Task GetMetadataById_DatabaseNotFound_ReturnsNotFound()
        {
            // Arrange
            string databaseId = "nonexistentdb";
            string tableId = "table1";

            _mockDbConnector.Setup(x => x.GetDataBaseReference(databaseId)).Returns((DataBaseRef?)null);

            // Act
            ActionResult<JsonStat2> result = await _controller.GetTableMetadataById(databaseId, tableId, null);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<NotFoundObjectResult>());
            NotFoundObjectResult? notFoundResult = result.Result as NotFoundObjectResult;
            Assert.That(notFoundResult?.Value, Is.EqualTo("Database not found."));
        }

        [Test]
        public async Task GetMetadataById_TableNotFound_ReturnsNotFound()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("exampledb");
            string tableId = "nonexistenttable";

            _mockDbConnector.Setup(x => x.GetDataBaseReference(database.Id)).Returns(database);
            _mockDbConnector.Setup(x => x.GetFileReferenceCachedAsync(tableId, database)).ReturnsAsync((PxFileRef?)null);

            // Act
            ActionResult<JsonStat2> result = await _controller.GetTableMetadataById(database.Id, tableId, null);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<NotFoundObjectResult>());
            NotFoundObjectResult? notFoundResult = result.Result as NotFoundObjectResult;
            Assert.That(notFoundResult?.Value, Is.EqualTo("Table not found."));
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
            Assert.That(result.Result, Is.InstanceOf<NotFoundObjectResult>());
        }

        [Test]
        public async Task GetMetadataById_UnexpectedError_ReturnsInternalServerError()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("exampledb");
            PxFileRef file = PxFileRef.CreateFromPath(Path.Combine("c:", "testfolder", "filename.px"), database);

            _mockDbConnector.Setup(x => x.GetDataBaseReference(database.Id)).Returns(database);
            _mockDbConnector.Setup(x => x.GetFileReferenceCachedAsync(file.Id, database)).ReturnsAsync(file);
            _mockDbConnector.Setup(x => x.GetMetadataCachedAsync(file)).ThrowsAsync(new InvalidOperationException("Unexpected error"));

            // Act
            ActionResult<JsonStat2> result = await _controller.GetTableMetadataById(database.Id, file.Id, null);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<ObjectResult>());
            ObjectResult? objectResult = result.Result as ObjectResult;
            Assert.Multiple(() =>
            {
                Assert.That(objectResult?.StatusCode, Is.EqualTo(500));
                Assert.That(objectResult?.Value, Is.EqualTo("Unexpected server error."));
            });
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
            string[] expectedId = ["content-code", "time-code", "dim0-code", "dim1-code"];


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
                Assert.That(resultMeta.Id, Is.EqualTo(expectedId));
                Assert.That(resultMeta.Label, Is.EqualTo("table-description.fi"));
                Assert.That(resultMeta.Source, Is.EqualTo("table-source.fi"));
                Assert.That(resultMeta.Dimension, Has.Count.EqualTo(4));
                Assert.That(resultMeta.Size, Has.Count.EqualTo(4));
            });
        }

        [Test]
        public async Task HeadMetadataAsync_ResourceExists_ReturnsOk()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("exampledb");
            PxFileRef file = PxFileRef.CreateFromPath(Path.Combine("c:", "testfolder", "filename.px"), database);
            string lang = "en";
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();

            _mockDbConnector.Setup(x => x.GetDataBaseReference(database.Id)).Returns(database);
            _mockDbConnector.Setup(x => x.GetFileReferenceCachedAsync(file.Id, database)).ReturnsAsync(file);
            _mockDbConnector.Setup(x => x.GetMetadataCachedAsync(file)).ReturnsAsync(meta);

            // Act
            IActionResult result = await _controller.HeadMetadataAsync(database.Id, file.Id, lang);

            // Assert
            Assert.That(result, Is.InstanceOf<OkResult>());
        }

        [Test]
        public async Task HeadMetadataAsync_DatabaseNotFound_ReturnsNotFound()
        {
            // Arrange
            string databaseId = "nonexistentdb";
            string tableId = "table1";

            _mockDbConnector.Setup(x => x.GetDataBaseReference(databaseId)).Returns((DataBaseRef?)null);

            // Act
            IActionResult result = await _controller.HeadMetadataAsync(databaseId, tableId, null);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundResult>());
        }

        [Test]
        public async Task HeadMetadataAsync_TableNotFound_ReturnsNotFound()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("exampledb");
            string tableId = "nonexistenttable";

            _mockDbConnector.Setup(x => x.GetDataBaseReference(database.Id)).Returns(database);
            _mockDbConnector.Setup(x => x.GetFileReferenceCachedAsync(tableId, database)).ReturnsAsync((PxFileRef?)null);

            // Act
            IActionResult result = await _controller.HeadMetadataAsync(database.Id, tableId, null);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundResult>());
        }

        [Test]
        public async Task HeadMetadataAsync_LanguageNotAvailable_ReturnsBadRequest()
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
            IActionResult result = await _controller.HeadMetadataAsync(database.Id, file.Id, lang);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestResult>());
        }

        [Test]
        public async Task HeadMetadataAsync_NoLanguageSpecified_ReturnsOk()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("exampledb");
            PxFileRef file = PxFileRef.CreateFromPath(Path.Combine("c:", "testfolder", "filename.px"), database);
            MatrixMetadata meta = TestMockMetaBuilder.GetMockMetadata();

            _mockDbConnector.Setup(x => x.GetDataBaseReference(database.Id)).Returns(database);
            _mockDbConnector.Setup(x => x.GetFileReferenceCachedAsync(file.Id, database)).ReturnsAsync(file);
            _mockDbConnector.Setup(x => x.GetMetadataCachedAsync(file)).ReturnsAsync(meta);

            // Act
            IActionResult result = await _controller.HeadMetadataAsync(database.Id, file.Id, null);

            // Assert
            Assert.That(result, Is.InstanceOf<OkResult>());
        }

        [Test]
        public void OptionsMetadata_ReturnsOkWithAllowHeader()
        {
            // Arrange
            string database = "exampledb";
            string table = "table1";

            // Act
            IActionResult result = _controller.OptionsMetadata(database, table);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.InstanceOf<OkResult>());
                Assert.That(_controller.Response.Headers.Allow.ToString(), Is.EqualTo("GET,HEAD,OPTIONS"));
            });
        }
    }
}
