using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Px.Utils.Models.Metadata.ExtensionMethods;
using Px.Utils.Models.Metadata;
using PxApi.Configuration;
using PxApi.Controllers;
using PxApi.DataSources;
using PxApi.Models;
using PxApi.UnitTests.ModelBuilderTests;
using System.Collections.Immutable;

namespace PxApi.UnitTests.ControllerTests
{
    [TestFixture]
    public class TablesControllerTests
    {
        private Mock<IDataSource> _mockDataSource;
        private Mock<ILogger<TablesController>> _mockLogger;
        private TablesController _controller;

        [SetUp]
        public void SetUp()
        {
            _mockDataSource = new Mock<IDataSource>();
            _mockLogger = new Mock<ILogger<TablesController>>();
            _controller = new TablesController(_mockDataSource.Object, _mockLogger.Object)
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
                    {"DataSource:LocalFileSystem:MetadataCache:AbsoluteExpirationMinutes", "15"}
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
            string dbId = "example-db";
            string lang = "en";
            int page = 1;
            int pageSize = 50;
            PxTable table1 = new("table1.px", ["hierarchy1"], dbId);
            PxTable table2 = new("table2.px", ["hierarchy2"], dbId);
            ImmutableSortedDictionary<string, PxTable> tableList = ImmutableSortedDictionary.CreateRange(new Dictionary<string, PxTable>
            {
                { "table1.px", table1 },
                { "table2.px", table2 }
            });
            MatrixMetadata meta1 = TestMockMetaBuilder.GetMockMetadata();
            MatrixMetadata meta2 = TestMockMetaBuilder.GetMockMetadata();

            _mockDataSource.Setup(ds => ds.GetSortedTableDictCachedAsync(dbId)).ReturnsAsync(tableList);
            _mockDataSource.Setup(ds => ds.GetMatrixMetadataCachedAsync(table1)).ReturnsAsync(meta1);
            _mockDataSource.Setup(ds => ds.GetMatrixMetadataCachedAsync(table2)).ReturnsAsync(meta2);

            // Act
            ActionResult<PagedTableList> result = await _controller.GetTablesAsync(dbId, lang, page, pageSize);

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
                Assert.That(pagedTableList.Tables[0].Name, Is.EqualTo("table1.px"));
                Assert.That(pagedTableList.Tables[0].LastUpdated, Is.EqualTo(meta1.GetContentDimension().Values.Map(v => v.LastUpdated).Max()));
                Assert.That(pagedTableList.Tables[0].Links, Has.Count.EqualTo(1));
                Assert.That(pagedTableList.Tables[0].Links[0].Rel, Is.EqualTo("describedby"));
                Assert.That(pagedTableList.Tables[0].Links[0].Href, Is.EqualTo("https://testurl.fi/meta/example-db/table1.px?lang=en"));
                Assert.That(pagedTableList.Tables[0].Links[0].Method, Is.EqualTo("GET"));
            });
        }

        [Test]
        public async Task GetTablesAsync_InvalidPage_ReturnsBadRequest()
        {
            // Arrange
            string dbId = "example-db";
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
            string dbId = "example-db";
            string lang = "en";
            int page = 1;
            int pageSize = 200;
            ImmutableSortedDictionary<string, PxTable> tableList = ImmutableSortedDictionary<string, PxTable>.Empty;
            _mockDataSource.Setup(ds => ds.GetSortedTableDictCachedAsync(dbId)).ReturnsAsync(tableList);

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
            string dbId = "nonexistent-db";
            string lang = "en";
            int page = 1;
            int pageSize = 50;
            _mockDataSource.Setup(ds => ds.GetSortedTableDictCachedAsync(dbId)).ThrowsAsync(new DirectoryNotFoundException());

            // Act
            ActionResult<PagedTableList> result = await _controller.GetTablesAsync(dbId, lang, page, pageSize);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<NotFoundResult>());
        }

        [Test]
        public async Task GetTablesAsync_PagingWorksCorrectly()
        {
            // Arrange
            string dbId = "example-db";
            string lang = "en";
            int pageSize = 3;
            List<PxTable> tables =
            [
                new PxTable("table1.px", ["hierarchy1"], dbId),
                new PxTable("table2.px", ["hierarchy1"], dbId),
                new PxTable("table3.px", ["hierarchy1"], dbId),
                new PxTable("table4.px", ["hierarchy2"], dbId),
                new PxTable("table5.px", ["hierarchy2"], dbId),
                new PxTable("table6.px", ["hierarchy3"], dbId),
                new PxTable("table7.px", ["hierarchy4"], dbId),
            ];
            ImmutableSortedDictionary<string, PxTable> tableList = ImmutableSortedDictionary.CreateRange(tables.ToDictionary(t => t.TableId));
            _mockDataSource.Setup(ds => ds.GetSortedTableDictCachedAsync(dbId)).ReturnsAsync(tableList);
            _mockDataSource.Setup(ds => ds.GetMatrixMetadataCachedAsync(It.IsAny<PxTable>())).ReturnsAsync(TestMockMetaBuilder.GetMockMetadata());

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
                        Assert.That(pagedTableList.Tables[i].Name, Is.EqualTo(tables[tableIndex++].TableId));
                    }
                });
            }
        }
    }
}
