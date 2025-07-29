using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Px.Utils.Models.Metadata.ExtensionMethods;
using Px.Utils.Models.Metadata;
using PxApi.Caching;
using PxApi.Configuration;
using PxApi.Controllers;
using PxApi.ModelBuilders;
using PxApi.Models;
using PxApi.UnitTests.ModelBuilderTests;
using System.Collections.Immutable;

namespace PxApi.UnitTests.ControllerTests
{
    [TestFixture]
    public class TablesControllerTests
    {
        private Mock<ICachedDataBaseConnector> _cachedDbConnector;
        private Mock<ILogger<TablesController>> _mockLogger;
        private TablesController _controller;

        [SetUp]
        public void SetUp()
        {
            _cachedDbConnector = new Mock<ICachedDataBaseConnector>();
            _mockLogger = new Mock<ILogger<TablesController>>();
            _controller = new TablesController(_cachedDbConnector.Object, _mockLogger.Object)
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
        public async Task GetTablesAsync_TwoTables_ReturnsExpectedMetadata()
        {
            // Arrange
            DataBaseRef db = DataBaseRef.Create("exampledb");
            string lang = "en";
            int page = 1;
            int pageSize = 50;
            PxFileRef file1 = PxFileRef.Create("file1", db);
            PxFileRef file2 = PxFileRef.Create("file2", db);
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
            Assert.That(result.Result, Is.InstanceOf<BadRequestResult>());
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
        public async Task GetTablesAsync_PagingWorksCorrectly()
        {
            // Arrange
            string dbId = "exampledb";
            DataBaseRef db = DataBaseRef.Create(dbId);
            string lang = "en";
            int pageSize = 3;
            
            List<PxFileRef> files =
            [
                PxFileRef.Create("file1", db),
                PxFileRef.Create("file2", db),
                PxFileRef.Create("file3", db),
                PxFileRef.Create("file4", db),
                PxFileRef.Create("file5", db),
                PxFileRef.Create("file6", db),
                PxFileRef.Create("file7", db),
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
            
            PxFileRef file = PxFileRef.Create("table1", db);
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
            
            PxFileRef file = PxFileRef.Create("table1", db);
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
    }
}
