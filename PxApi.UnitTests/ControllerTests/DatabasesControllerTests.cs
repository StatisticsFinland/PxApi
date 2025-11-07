using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Px.Utils.Language;
using PxApi.Caching;
using PxApi.Configuration;
using PxApi.Controllers;
using PxApi.Models;
using PxApi.Services;
using PxApi.UnitTests.Utils;
using System.Collections.Immutable;

namespace PxApi.UnitTests.ControllerTests
{
    [TestFixture]
    public class DatabasesControllerTests
    {
        private Mock<ICachedDataSource> _mockCachedDataSource = null!;
        private Mock<IAuditLogService> _mockAuditLogger = null!; // Added
        private DatabasesController _controller = null!;

        [SetUp]
        public void SetUp()
        {
            _mockCachedDataSource = new Mock<ICachedDataSource>();
            _mockAuditLogger = new Mock<IAuditLogService>();
            _controller = new DatabasesController(_mockCachedDataSource.Object, _mockAuditLogger.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };

            // Default config with two databases (no descriptions). Tests can override by re-loading AppSettings.
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
            TestConfigFactory.Base(),
            TestConfigFactory.MountedDb(0, "db1", "datasource/root1/"),
            TestConfigFactory.MountedDb(1, "db2", "datasource/root2/")
            );
            TestConfigFactory.BuildAndLoad(configData);
        }

        [Test]
        public async Task GetDatabases_ValidRequest_LogsAuditEvent()
        {
            // Arrange
            DataBaseRef dbRef = DataBaseRef.Create("db1");
            List<DataBaseRef> dbRefs = [dbRef];
            _mockCachedDataSource.Setup(x => x.GetAllDataBaseReferences()).Returns(dbRefs);
            MultilanguageString nameMulti = new(new Dictionary<string, string>
            {
                { "fi", "Nimi FI" },
                { "en", "Name EN" }
            });
            _mockCachedDataSource.Setup(x => x.GetDatabaseNameAsync(dbRef, string.Empty)).ReturnsAsync(nameMulti);
            ImmutableSortedDictionary<string, PxFileRef> files = ImmutableSortedDictionary<string, PxFileRef>.Empty;
            _mockCachedDataSource.Setup(x => x.GetFileListCachedAsync(dbRef)).ReturnsAsync(files);

            // Act
            ActionResult<List<DataBaseListingItem>> result = await _controller.GetDatabases("fi");

            // Assert
            Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
            _mockAuditLogger.Verify(x => x.LogAuditEvent("GetDatabases", "databases"), Times.Once);
        }

        [Test]
        public void HeadDatabases_LogsAuditEvent()
        {
            // Act
            IActionResult result = _controller.HeadDatabases();

            // Assert
            Assert.That(result, Is.InstanceOf<OkResult>());
            _mockAuditLogger.Verify(x => x.LogAuditEvent("HeadDatabases", "databases"), Times.Once);
        }

        [Test]
        public void OptionsDatabases_LogsAuditEvent()
        {
            // Act
            IActionResult result = _controller.OptionsDatabases();

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.InstanceOf<OkResult>());
                Assert.That(_controller.Response.Headers.Allow, Is.EqualTo("GET,HEAD,OPTIONS"));
            });
            _mockAuditLogger.Verify(x => x.LogAuditEvent("OptionsDatabases", "databases"), Times.Once);
        }

        [Test]
        public async Task GetDatabases_InvalidLanguage_ReturnsBadRequest()
        {
            // Arrange
            string invalidLang = "de"; // Not in SupportedLanguages (fi, sv, en)
            List<DataBaseRef> dbRefs = [DataBaseRef.Create("db1")];
            _mockCachedDataSource.Setup(x => x.GetAllDataBaseReferences()).Returns(dbRefs);

            // Act
            ActionResult<List<DataBaseListingItem>> result = await _controller.GetDatabases(invalidLang);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task GetDatabases_NoLangParameter_UsesDefaultLanguageFromSettings()
        {
            // Arrange
            DataBaseRef dbRef = DataBaseRef.Create("db1");
            List<DataBaseRef> dbRefs = [dbRef];
            _mockCachedDataSource.Setup(x => x.GetAllDataBaseReferences()).Returns(dbRefs);

            MultilanguageString nameMulti = new(new Dictionary<string, string>
            {
                { "fi", "Nimi FI" },
                { "en", "Name EN" }
            });
            _mockCachedDataSource.Setup(x => x.GetDatabaseNameAsync(dbRef, It.Is<string>(s => s == string.Empty))).ReturnsAsync(nameMulti);
            ImmutableSortedDictionary<string, PxFileRef> fileList = ImmutableSortedDictionary<string, PxFileRef>.Empty;
            _mockCachedDataSource.Setup(x => x.GetFileListCachedAsync(dbRef)).ReturnsAsync(fileList);

            // Act
            ActionResult<List<DataBaseListingItem>> result = await _controller.GetDatabases(null);

            // Assert
            OkObjectResult? okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            List<DataBaseListingItem>? items = okResult!.Value as List<DataBaseListingItem>;
            Assert.Multiple(() =>
            {
                Assert.That(items, Is.Not.Null);
                Assert.That(items!, Has.Count.EqualTo(1));
                Assert.That(items![0].Name, Is.EqualTo("Nimi FI")); // Default language fi
                Assert.That(items![0].AvailableLanguages, Is.EquivalentTo(new List<string> { "fi", "en" }));
            });
            _mockCachedDataSource.Verify(x => x.GetDatabaseNameAsync(dbRef, string.Empty), Times.Once);
            _mockCachedDataSource.Verify(x => x.GetFileListCachedAsync(dbRef), Times.Once);
        }

        [Test]
        public async Task GetDatabases_LanguageWithoutDescription_ReturnsNullDescription()
        {
            // Arrange: provide description only for fi, request sv
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
            TestConfigFactory.Base(),
            TestConfigFactory.MountedDb(0, "db1", "datasource/root1/"),
            new Dictionary<string, string?>
            {
                ["DataBases:0:Custom:Description.fi"] = "Kuvaus FI"
            }
            );
            TestConfigFactory.BuildAndLoad(configData);

            string requestedLang = "sv"; // Swedish, description not provided
            DataBaseRef dbRef = DataBaseRef.Create("db1");
            List<DataBaseRef> dbRefs = [dbRef];
            _mockCachedDataSource.Setup(x => x.GetAllDataBaseReferences()).Returns(dbRefs);

            MultilanguageString nameMulti = new(new Dictionary<string, string>
            {
                { "fi", "Nimi FI" },
                { "sv", "Namn SV" },
                { "en", "Name EN" }
            });
            _mockCachedDataSource.Setup(x => x.GetDatabaseNameAsync(dbRef, string.Empty)).ReturnsAsync(nameMulti);
            ImmutableSortedDictionary<string, PxFileRef> fileList = ImmutableSortedDictionary<string, PxFileRef>.Empty;
            _mockCachedDataSource.Setup(x => x.GetFileListCachedAsync(dbRef)).ReturnsAsync(fileList);

            // Act
            ActionResult<List<DataBaseListingItem>> result = await _controller.GetDatabases(requestedLang);

            // Assert
            OkObjectResult? okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            List<DataBaseListingItem>? items = okResult!.Value as List<DataBaseListingItem>;
            Assert.Multiple(() =>
            {
                Assert.That(items![0].Description, Is.Null);
                Assert.That(items[0].Name, Is.EqualTo("Namn SV"));
            });
        }

        [Test]
        public async Task GetDatabases_MultipleDatabases_ReturnsExpectedListing()
        {
            // Arrange: build configuration with English descriptions
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
            TestConfigFactory.Base(),
            TestConfigFactory.MountedDb(0, "db1", "datasource/root1/"),
            TestConfigFactory.MountedDb(1, "db2", "datasource/root2/"),
            new Dictionary<string, string?>
            {
                ["DataBases:0:Custom:Description.en"] = "Description EN1",
                ["DataBases:1:Custom:Description.en"] = "Description EN2"
            }
            );
            TestConfigFactory.BuildAndLoad(configData);

            string lang = "en";
            DataBaseRef dbRef1 = DataBaseRef.Create("db1");
            DataBaseRef dbRef2 = DataBaseRef.Create("db2");
            List<DataBaseRef> dbRefs = [dbRef1, dbRef2];
            _mockCachedDataSource.Setup(x => x.GetAllDataBaseReferences()).Returns(dbRefs);

            MultilanguageString nameMulti1 = new(new Dictionary<string, string>
            {
                { "fi", "nimi1" },
                { "sv", "namn1" },
                { "en", "name1" }
            });
            MultilanguageString nameMulti2 = new(new Dictionary<string, string>
            {
                { "fi", "nimi2" },
                { "en", "name2" } // Only fi & en
            });
            _mockCachedDataSource.Setup(x => x.GetDatabaseNameAsync(dbRef1, string.Empty)).ReturnsAsync(nameMulti1);
            _mockCachedDataSource.Setup(x => x.GetDatabaseNameAsync(dbRef2, string.Empty)).ReturnsAsync(nameMulti2);

            PxFileRef px1a = PxFileRef.CreateFromPath(Path.Combine("c:", "test", "t1a.px"), dbRef1);
            PxFileRef px1b = PxFileRef.CreateFromPath(Path.Combine("c:", "test", "t1b.px"), dbRef1);
            ImmutableSortedDictionary<string, PxFileRef> filesDb1 = ImmutableSortedDictionary.CreateRange(new Dictionary<string, PxFileRef>
            {
                { px1a.Id, px1a },
                { px1b.Id, px1b }
            });
            _mockCachedDataSource.Setup(x => x.GetFileListCachedAsync(dbRef1)).ReturnsAsync(filesDb1);

            PxFileRef px2a = PxFileRef.CreateFromPath(Path.Combine("c:", "test", "t2a.px"), dbRef2);
            ImmutableSortedDictionary<string, PxFileRef> filesDb2 = ImmutableSortedDictionary.CreateRange(new Dictionary<string, PxFileRef>
            {
                { px2a.Id, px2a }
            });
            _mockCachedDataSource.Setup(x => x.GetFileListCachedAsync(dbRef2)).ReturnsAsync(filesDb2);

            // Act
            ActionResult<List<DataBaseListingItem>> result = await _controller.GetDatabases(lang);

            // Assert
            OkObjectResult? okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            List<DataBaseListingItem>? items = okResult!.Value as List<DataBaseListingItem>;
            Assert.Multiple(() =>
            {
                Assert.That(items, Is.Not.Null);
                Assert.That(items!, Has.Count.EqualTo(2));
                // db1
                DataBaseListingItem item1 = items!.First(i => i.ID == "db1");
                Assert.That(item1.Name, Is.EqualTo("name1"));
                Assert.That(item1.Description, Is.EqualTo("Description EN1"));
                Assert.That(item1.TableCount, Is.EqualTo(2));
                Assert.That(item1.AvailableLanguages, Is.EquivalentTo(new List<string> { "fi", "sv", "en" }));
                Assert.That(item1.Links, Has.Count.EqualTo(1));
                Assert.That(item1.Links[0].Rel, Is.EqualTo("describedby"));
                Assert.That(item1.Links[0].Method, Is.EqualTo("GET"));
                string expectedHref1 = AppSettings.Active.RootUrl.ToString().TrimEnd('/') + "/tables/db1?lang=en";
                Assert.That(item1.Links[0].Href, Is.EqualTo(expectedHref1));
                // db2
                DataBaseListingItem item2 = items!.First(i => i.ID == "db2");
                Assert.That(item2.Name, Is.EqualTo("name2"));
                Assert.That(item2.Description, Is.EqualTo("Description EN2"));
                Assert.That(item2.TableCount, Is.EqualTo(1));
                Assert.That(item2.AvailableLanguages, Is.EquivalentTo(new List<string> { "fi", "en" }));
                string expectedHref2 = AppSettings.Active.RootUrl.ToString().TrimEnd('/') + "/tables/db2?lang=en";
                Assert.That(item2.Links[0].Href, Is.EqualTo(expectedHref2));
            });
            _mockCachedDataSource.Verify(x => x.GetDatabaseNameAsync(dbRef1, string.Empty), Times.Once);
            _mockCachedDataSource.Verify(x => x.GetDatabaseNameAsync(dbRef2, string.Empty), Times.Once);
            _mockCachedDataSource.Verify(x => x.GetFileListCachedAsync(dbRef1), Times.Once);
            _mockCachedDataSource.Verify(x => x.GetFileListCachedAsync(dbRef2), Times.Once);
        }

        [Test]
        public void HeadDatabases_ReturnsOk()
        {
            // Act
            IActionResult result = _controller.HeadDatabases();

            // Assert
            Assert.That(result, Is.InstanceOf<OkResult>());
        }

        [Test]
        public void OptionsDatabases_SetsAllowHeader_ReturnsOk()
        {
            // Act
            IActionResult result = _controller.OptionsDatabases();

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.InstanceOf<OkResult>());
                Assert.That(_controller.Response.Headers.Allow, Is.EqualTo("GET,HEAD,OPTIONS"));
            });
        }
    }
}
