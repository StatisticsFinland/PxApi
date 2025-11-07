using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Px.Utils.Models.Metadata.Dimensions;
using Px.Utils.Models.Metadata.ExtensionMethods;
using Px.Utils.Models.Metadata;
using PxApi.Caching;
using PxApi.Controllers;
using PxApi.ModelBuilders;
using PxApi.Models;
using PxApi.Services;
using PxApi.UnitTests.ModelBuilderTests;
using PxApi.UnitTests.Utils;
using System.Collections.Immutable;

namespace PxApi.UnitTests.ControllerTests
{
    [TestFixture]
    public class TablesControllerTests
    {
        private Mock<ICachedDataSource> _cachedDbConnector;
        private Mock<ILogger<TablesController>> _mockLogger;
        private Mock<IAuditLogService> _mockAuditLogger; // Added mock for audit service
        private TablesController _controller;

        [SetUp]
        public void SetUp()
        {
            _cachedDbConnector = new Mock<ICachedDataSource>();
            _mockLogger = new Mock<ILogger<TablesController>>();
            _mockAuditLogger = new Mock<IAuditLogService>();
            _controller = new TablesController(_cachedDbConnector.Object, _mockLogger.Object, _mockAuditLogger.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };

            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                TestConfigFactory.MountedDb(0, "exampledb", "datasource/root/"),
                new Dictionary<string, string?>
                {
                    ["DataBases:0:CacheConfig:Modifiedtime:SlidingExpirationSeconds"] = "60",
                    ["DataBases:0:CacheConfig:Modifiedtime:AbsoluteExpirationSeconds"] = "60",
                    ["DataBases:0:Custom:ModifiedCheckIntervalMs"] = "1000",
                    ["DataBases:0:Custom:FileListingCacheDurationMs"] = "10000"
                }
            );
            TestConfigFactory.BuildAndLoad(configData);
        }

        [Test]
        public async Task GetTablesAsync_ValidRequest_LogsAuditEvent()
        {
            // Arrange
            DataBaseRef db = DataBaseRef.Create("exampledb");
            string lang = "en";
            int page = 1;
            int pageSize = 50;
            _cachedDbConnector.Setup(ds => ds.GetDataBaseReference(db.Id)).Returns(db);
            ImmutableSortedDictionary<string, PxFileRef> tableList = ImmutableSortedDictionary<string, PxFileRef>.Empty;
            _cachedDbConnector.Setup(ds => ds.GetFileListCachedAsync(db)).ReturnsAsync(tableList);

            // Act
            ActionResult<PagedTableList> result = await _controller.GetTablesAsync(db.Id, lang, page, pageSize);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
            _mockAuditLogger.Verify(x => x.LogAuditEvent("GetTablesAsync", db.Id), Times.Once);
        }

        [Test]
        public void HeadTablesAsync_ValidParameters_LogsAuditEvent()
        {
            // Arrange
            string database = "exampledb";
            DataBaseRef db = DataBaseRef.Create(database);
            int page = 1;
            int pageSize = 50;
            _cachedDbConnector.Setup(ds => ds.GetDataBaseReference(database)).Returns(db);

            // Act
            IActionResult result = _controller.HeadTablesAsync(database, page, pageSize);

            // Assert
            Assert.That(result, Is.InstanceOf<OkResult>());
            _mockAuditLogger.Verify(x => x.LogAuditEvent("HeadTablesAsync", db.Id), Times.Once);
        }

        [Test]
        public void OptionsTables_AnyDatabase_LogsAuditEvent()
        {
            // Arrange
            string database = "exampledb";
            DataBaseRef db = DataBaseRef.Create(database);
            _cachedDbConnector.Setup(ds => ds.GetDataBaseReference(database)).Returns(db);

            // Act
            IActionResult result = _controller.OptionsTables(database);

            // Assert
            Assert.That(result, Is.InstanceOf<OkResult>());
            _mockAuditLogger.Verify(x => x.LogAuditEvent("OptionsTables", db.Id), Times.Once);
        }

        [Test]
        public async Task GetTablesAsync_TwoTables_ReturnsExpectedMetadata()
        {
            // Arrange
            DataBaseRef db = DataBaseRef.Create("exampledb");
            string lang = "en";
            int page = 1;
            int pageSize = 50;
            PxFileRef file1 = PxFileRef.CreateFromPath(Path.Combine("c:", "testfolder", "file1.px"), db);
            PxFileRef file2 = PxFileRef.CreateFromPath(Path.Combine("c:", "testfolder", "file2.px"), db);
            ImmutableSortedDictionary<string, PxFileRef> tableList = ImmutableSortedDictionary.CreateRange(new Dictionary<string, PxFileRef>
            {
                { "file1", file1 },
                { "file2", file2 }
            });
            MatrixMetadata meta1 = TestMockMetaBuilder.GetMockMetadata();
            MatrixMetadata meta2 = TestMockMetaBuilder.GetMockMetadata();

            _cachedDbConnector.Setup(ds => ds.GetDataBaseReference(db.Id)).Returns(db);
            _cachedDbConnector.Setup(ds => ds.GetFileListCachedAsync(db)).ReturnsAsync(tableList);
            _cachedDbConnector.Setup(ds => ds.GetMetadataCachedAsync(file1)).ReturnsAsync(meta1);
            _cachedDbConnector.Setup(ds => ds.GetMetadataCachedAsync(file2)).ReturnsAsync(meta2);

            // Act
            ActionResult<PagedTableList> result = await _controller.GetTablesAsync(db.Id, lang, page, pageSize);

            // Assert
            Assert.That(result, Is.InstanceOf<ActionResult<PagedTableList>>());
            OkObjectResult? okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            PagedTableList? pagedTableList = okResult.Value as PagedTableList;
            Assert.That(pagedTableList, Is.Not.Null);
            Assert.That(pagedTableList.Tables, Has.Count.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(pagedTableList.Tables[0].ID, Is.EqualTo("table-tableid"));
                Assert.That(pagedTableList.Tables[0].Title, Is.EqualTo("table-description.en"));
                Assert.That(pagedTableList.Tables[0].Name, Is.EqualTo("file1"));
                Assert.That(pagedTableList.Tables[0].LastUpdated, Is.EqualTo(meta1.GetContentDimension().Values.Map(v => v.LastUpdated).Max()));
                Assert.That(pagedTableList.Tables[0].Links, Has.Count.EqualTo(1));
                Assert.That(pagedTableList.Tables[0].Links[0].Rel, Is.EqualTo("describedby"));
                Assert.That(pagedTableList.Tables[0].Links[0].Href, Is.EqualTo("https://testurl.fi/meta/exampledb/file1?lang=en"));
                Assert.That(pagedTableList.Tables[0].Links[0].Method, Is.EqualTo("GET"));
            });
        }

        [Test]
        public async Task GetTablesAsync_InvalidPage_ReturnsBadRequest()
        {
            // Arrange
            string dbId = "exampledb";
            string lang = "en";
            int page = 0;
            int pageSize = 50;

            // Act
            ActionResult<PagedTableList> result = await _controller.GetTablesAsync(dbId, lang, page, pageSize);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task GetTablesAsync_PageSizeExceedsMax_ReturnsPagedTableListWithMaxPageSize()
        {
            // Arrange
            string dbId = "exampledb";
            string lang = "en";
            int page = 1;
            int pageSize = 200;
            DataBaseRef db = DataBaseRef.Create(dbId);
            ImmutableSortedDictionary<string, PxFileRef> tableList = ImmutableSortedDictionary<string, PxFileRef>.Empty;
            
            _cachedDbConnector.Setup(ds => ds.GetDataBaseReference(dbId)).Returns(db);
            _cachedDbConnector.Setup(ds => ds.GetFileListCachedAsync(db)).ReturnsAsync(tableList);

            // Act
            ActionResult<PagedTableList> result = await _controller.GetTablesAsync(dbId, lang, page, pageSize);

            // Assert
            Assert.That(result, Is.InstanceOf<ActionResult<PagedTableList>>());
            OkObjectResult? okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            PagedTableList? pagedTableList = okResult.Value as PagedTableList;
            Assert.That(pagedTableList, Is.Not.Null);
            Assert.That(pagedTableList.PagingInfo.PageSize, Is.EqualTo(100));
        }

        [Test]
        public async Task GetTablesAsync_DatabaseNotFound_ReturnsNotFound()
        {
            // Arrange
            string dbId = "nonexistentdb";
            string lang = "en";
            int page = 1;
            int pageSize = 50;
            
            _cachedDbConnector.Setup(ds => ds.GetDataBaseReference(dbId)).Returns((DataBaseRef?)null);

            // Act
            ActionResult<PagedTableList> result = await _controller.GetTablesAsync(dbId, lang, page, pageSize);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<NotFoundObjectResult>());
        }

        [Test]
        public async Task GetTablesAsync_DirectoryNotFound_ReturnsNotFound()
        {
            // Arrange
            string dbId = "exampledb";
            DataBaseRef db = DataBaseRef.Create(dbId);
            string lang = "en";
            int page = 1;
            int pageSize = 50;
            
            _cachedDbConnector.Setup(ds => ds.GetDataBaseReference(dbId)).Returns(db);
            _cachedDbConnector.Setup(ds => ds.GetFileListCachedAsync(db)).ThrowsAsync(new DirectoryNotFoundException("Directory not found"));

            // Act
            ActionResult<PagedTableList> result = await _controller.GetTablesAsync(dbId, lang, page, pageSize);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<NotFoundResult>());
        }

        [Test]
        public async Task GetTablesAsync_PagingWorksCorrectly()
        {
            // Arrange
            string dbId = "exampledb";
            DataBaseRef db = DataBaseRef.Create(dbId);
            string lang = "en";
            int pageSize = 3;
            
            List<PxFileRef> files =
            [
                PxFileRef.CreateFromPath(Path.Combine("c:", "testfolder", "file1.px"), db),
                PxFileRef.CreateFromPath(Path.Combine("c:", "testfolder", "file2.px"), db),
                PxFileRef.CreateFromPath(Path.Combine("c:", "testfolder", "file3.px"), db),
                PxFileRef.CreateFromPath(Path.Combine("c:", "testfolder", "file4.px"), db),
                PxFileRef.CreateFromPath(Path.Combine("c:", "testfolder", "file5.px"), db),
                PxFileRef.CreateFromPath(Path.Combine("c:", "testfolder", "file6.px"), db),
                PxFileRef.CreateFromPath(Path.Combine("c:", "testfolder", "file7.px"), db),
            ];
            
            ImmutableSortedDictionary<string, PxFileRef> tableList = ImmutableSortedDictionary.CreateRange(
                files.ToDictionary(f => f.Id));
            
            _cachedDbConnector.Setup(ds => ds.GetDataBaseReference(dbId)).Returns(db);
            _cachedDbConnector.Setup(ds => ds.GetFileListCachedAsync(db)).ReturnsAsync(tableList);
            
            foreach (PxFileRef file in files)
            {
                _cachedDbConnector.Setup(ds => ds.GetMetadataCachedAsync(file)).ReturnsAsync(TestMockMetaBuilder.GetMockMetadata());
            }

            int tableIndex = 0;

            // Act & Assert
            for (int page = 1; page <= 3; page++)
            {
                ActionResult<PagedTableList> result = await _controller.GetTablesAsync(dbId, lang, page, pageSize);
                Assert.That(result, Is.InstanceOf<ActionResult<PagedTableList>>());
                OkObjectResult? okResult = result.Result as OkObjectResult;
                Assert.That(okResult, Is.Not.Null);
                PagedTableList? pagedTableList = okResult.Value as PagedTableList;
                Assert.That(pagedTableList, Is.Not.Null);

                if(page == 3) Assert.That(pagedTableList.Tables, Has.Count.EqualTo(1));
                else Assert.That(pagedTableList.Tables, Has.Count.EqualTo(3));

                Assert.Multiple(() =>
                {
                    for (int i = 0; i < (page == 3 ? 1 : pageSize); i++)
                    {
                        Assert.That(pagedTableList.Tables[i].Name, Is.EqualTo(files[tableIndex++].Id));
                    }
                });

                Assert.That(pagedTableList.PagingInfo.CurrentPage, Is.EqualTo(page));
            }

            Assert.That(tableIndex, Is.EqualTo(files.Count));
        }

        [Test]
        public async Task GetTablesAsync_BuildingMetadataIsNotPossible_ReturnsTableObjectWithErrorState_ReadIdFromTable()
        {
            // Arrange
            string dbId = "exampledb";
            DataBaseRef db = DataBaseRef.Create(dbId);
            string lang = "en";
            int page = 1;
            int pageSize = 50;
            
            PxFileRef file = PxFileRef.CreateFromPath(Path.Combine("c:", "testfolder", "table1.px"), db);
            ImmutableSortedDictionary<string, PxFileRef> tableList = ImmutableSortedDictionary.CreateRange(
                new Dictionary<string, PxFileRef> { { file.Id, file } });

            _cachedDbConnector.Setup(ds => ds.GetDataBaseReference(dbId)).Returns(db);
            _cachedDbConnector.Setup(ds => ds.GetFileListCachedAsync(db)).ReturnsAsync(tableList);
            _cachedDbConnector.Setup(ds => ds.GetMetadataCachedAsync(file)).ThrowsAsync(new Exception("Metaobject build error!"));
            _cachedDbConnector.Setup(ds => ds.GetSingleStringValueAsync(PxFileConstants.TABLEID, file)).ReturnsAsync("\"table-tableid\"");

            // Act
            ActionResult<PagedTableList> result = await _controller.GetTablesAsync(dbId, lang, page, pageSize);

            // Assert
            Assert.That(result, Is.InstanceOf<ActionResult<PagedTableList>>());
            OkObjectResult? objectResult = result.Result as OkObjectResult;
            Assert.That(objectResult, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(objectResult.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
                if (objectResult.Value is not PagedTableList pageTableList)
                {
                    Assert.Fail("PagedTableList is null");
                }
                else
                {
                    Assert.That(pageTableList.Tables, Has.Count.EqualTo(1));
                    Assert.That(pageTableList.Tables[0].ID, Is.EqualTo("table-tableid"));
                    Assert.That(pageTableList.Tables[0].Name, Is.EqualTo("table1"));
                    Assert.That(pageTableList.Tables[0].Status, Is.EqualTo(TableStatus.Error));
                }
            });
        }

        [Test]
        public async Task GetTablesAsync_BuildingMetadataIsNotPossible_TableNotReadable_ReturnsTableObjectWithErrorState_TableIdIsName()
        {
            // Arrange
            string dbId = "exampledb";
            DataBaseRef db = DataBaseRef.Create(dbId);
            string lang = "en";
            int page = 1;
            int pageSize = 50;
            
            PxFileRef file = PxFileRef.CreateFromPath(Path.Combine("c:", "testfolder", "table1.px"), db);
            ImmutableSortedDictionary<string, PxFileRef> tableList = ImmutableSortedDictionary.CreateRange(
                new Dictionary<string, PxFileRef> { { file.Id, file } });

            _cachedDbConnector.Setup(ds => ds.GetDataBaseReference(dbId)).Returns(db);
            _cachedDbConnector.Setup(ds => ds.GetFileListCachedAsync(db)).ReturnsAsync(tableList);
            _cachedDbConnector.Setup(ds => ds.GetMetadataCachedAsync(file)).ThrowsAsync(new Exception("Metaobject build error!"));
            _cachedDbConnector.Setup(ds => ds.GetSingleStringValueAsync(PxFileConstants.TABLEID, file)).ThrowsAsync(new Exception("Table not readable!"));

            // Act
            ActionResult<PagedTableList> result = await _controller.GetTablesAsync(dbId, lang, page, pageSize);

            // Assert
            Assert.That(result, Is.InstanceOf<ActionResult<PagedTableList>>());
            OkObjectResult? objectResult = result.Result as OkObjectResult;
            Assert.That(objectResult, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(objectResult.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
                if (objectResult.Value is not PagedTableList pageTableList)
                {
                    Assert.Fail("PagedTableList is null");
                }
                else
                {
                    Assert.That(pageTableList.Tables, Has.Count.EqualTo(1));
                    Assert.That(pageTableList.Tables[0].ID, Is.EqualTo("table1"));
                    Assert.That(pageTableList.Tables[0].Name, Is.EqualTo("table1"));
                    Assert.That(pageTableList.Tables[0].Status, Is.EqualTo(TableStatus.Error));
                }
            });
        }

        [Test]
        public async Task GetTablesAsync_MetadataWithoutContentDimension_ReturnsTableWithErrorStatusAndNullLastUpdated()
        {
            // Arrange
            string dbId = "exampledb";
            DataBaseRef db = DataBaseRef.Create(dbId);
            string lang = "en";
            int page = 1;
            int pageSize = 50;
            
            PxFileRef file = PxFileRef.CreateFromPath(Path.Combine("c:", "testfolder", "table1.px"), db);
            ImmutableSortedDictionary<string, PxFileRef> tableList = ImmutableSortedDictionary.CreateRange(
                new Dictionary<string, PxFileRef> { { file.Id, file } });

            // Create metadata with only non-content dimensions (time dimension and regular dimensions)
            // This will cause TryGetContentDimension to return false
            MatrixMetadata metaWithoutContent = TestMockMetaBuilder.GetMockMetadata([
                Px.Utils.Models.Metadata.Enums.DimensionType.Ordinal, 
                Px.Utils.Models.Metadata.Enums.DimensionType.Nominal
            ]);

            // Remove the content dimension by creating a new metadata with only the other dimensions
            List<Dimension> dimensionsWithoutContent = [.. metaWithoutContent.Dimensions.Where(d => d is not ContentDimension)];

            MatrixMetadata metadataWithoutContentDim = new(
                metaWithoutContent.DefaultLanguage,
                metaWithoutContent.AvailableLanguages,
                dimensionsWithoutContent,
                metaWithoutContent.AdditionalProperties
            );

            _cachedDbConnector.Setup(ds => ds.GetDataBaseReference(dbId)).Returns(db);
            _cachedDbConnector.Setup(ds => ds.GetFileListCachedAsync(db)).ReturnsAsync(tableList);
            _cachedDbConnector.Setup(ds => ds.GetMetadataCachedAsync(file)).ReturnsAsync(metadataWithoutContentDim);

            // Act
            ActionResult<PagedTableList> result = await _controller.GetTablesAsync(dbId, lang, page, pageSize);

            // Assert
            Assert.That(result, Is.InstanceOf<ActionResult<PagedTableList>>());
            OkObjectResult? objectResult = result.Result as OkObjectResult;
            Assert.That(objectResult, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(objectResult.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
                if (objectResult.Value is not PagedTableList pageTableList)
                {
                    Assert.Fail("PagedTableList is null");
                }
                else
                {
                    Assert.That(pageTableList.Tables, Has.Count.EqualTo(1));
                    Assert.That(pageTableList.Tables[0].ID, Is.EqualTo("table-tableid"));
                    Assert.That(pageTableList.Tables[0].Name, Is.EqualTo("table1"));
                    Assert.That(pageTableList.Tables[0].Status, Is.EqualTo(TableStatus.Error));
                    Assert.That(pageTableList.Tables[0].LastUpdated, Is.EqualTo(DateTime.MinValue));
                }
            });
        }

        #region HeadTablesAsync Tests

        [Test]
        public void HeadTablesAsync_ValidParameters_ReturnsOk()
        {
            // Arrange
            string database = "exampledb";
            DataBaseRef db = DataBaseRef.Create(database);
            int page = 1;
            int pageSize = 50;

            _cachedDbConnector.Setup(ds => ds.GetDataBaseReference(database)).Returns(db);

            // Act
            IActionResult result = _controller.HeadTablesAsync(database, page, pageSize);

            // Assert
            Assert.That(result, Is.InstanceOf<OkResult>());
        }

        [Test]
        public void HeadTablesAsync_InvalidPage_ReturnsBadRequest()
        {
            // Arrange
            string database = "exampledb";
            int page = 0;
            int pageSize = 50;

            // Act
            IActionResult result = _controller.HeadTablesAsync(database, page, pageSize);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestResult>());
        }

        [Test]
        public void HeadTablesAsync_InvalidPageSize_ReturnsBadRequest()
        {
            // Arrange
            string database = "exampledb";
            int page = 1;
            int pageSize = 0;

            // Act
            IActionResult result = _controller.HeadTablesAsync(database, page, pageSize);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestResult>());
        }

        [Test]
        public void HeadTablesAsync_PageSizeExceedsMax_ReturnsBadRequest()
        {
            // Arrange
            string database = "exampledb";
            int page = 1;
            int pageSize = 101;

            // Act
            IActionResult result = _controller.HeadTablesAsync(database, page, pageSize);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestResult>());
        }

        [Test]
        public void HeadTablesAsync_DatabaseNotFound_ReturnsNotFound()
        {
            // Arrange
            string database = "nonexistentdb";
            int page = 1;
            int pageSize = 50;

            _cachedDbConnector.Setup(ds => ds.GetDataBaseReference(database)).Returns((DataBaseRef?)null);

            // Act
            IActionResult result = _controller.HeadTablesAsync(database, page, pageSize);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundResult>());
        }

        #endregion

        #region OptionsTables Tests

        [Test]
        public void OptionsTables_AnyDatabase_ReturnsOkWithAllowHeader()
        {
            // Arrange
            string database = "exampledb";
            DataBaseRef db = DataBaseRef.Create(database);
            _cachedDbConnector.Setup(ds => ds.GetDataBaseReference(database)).Returns(db);

            // Act
            IActionResult result = _controller.OptionsTables(database);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.InstanceOf<OkResult>());
                Assert.That(_controller.Response.Headers.Allow.ToString(), Is.EqualTo("GET,HEAD,OPTIONS"));
            });
        }

        #endregion
    }
}
