using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Px.Utils.Language;
using Px.Utils.PxFile.Data;
using PxApi.Caching;
using PxApi.DataSources;
using PxApi.Models;
using PxApi.UnitTests.Utils;
using System.Text;
using System.Text.Json;

namespace PxApi.UnitTests.Caching
{
    [TestFixture]
    internal class CachedDataSourceTests
    {
        private Mock<IDataBaseConnectorFactory> _connectorFactoryMock = null!;
        private Mock<IDataBaseConnector> _connectorMock = null!;
        private Mock<ILogger<CachedDataSource>> _loggerMock = null!;
        private DatabaseCache _cache = null!;
        private DataBaseRef _dbRef;

        [SetUp]
        public void SetUp()
        {
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                TestConfigFactory.MountedDb(0, "testdb", "C:/test"));
            TestConfigFactory.BuildAndLoad(configData);

            _connectorFactoryMock = new Mock<IDataBaseConnectorFactory>(MockBehavior.Strict);
            _connectorMock = new Mock<IDataBaseConnector>(MockBehavior.Strict);
            _loggerMock = new Mock<ILogger<CachedDataSource>>(MockBehavior.Loose);
            _dbRef = DataBaseRef.Create("testdb");

            MemoryCache memoryCache = new(new MemoryCacheOptions
            {
                SizeLimit = 1024 * 1024
            });
            _cache = new DatabaseCache(memoryCache);

            _connectorMock.SetupGet(connector => connector.DataBase).Returns(_dbRef);
            _connectorFactoryMock.Setup(factory => factory.GetConnector(It.Is<DataBaseRef>(db => db.Id == _dbRef.Id)))
                .Returns(_connectorMock.Object);
        }

        [Test]
        public async Task GetGroupingsCachedAsync_WhenGroupingFileMissing_ReturnsEmptyList()
        {
            // Arrange
            PxFileRef pxFile = PxFileRef.ValidateAndCreate("table1", _dbRef, ["level1"]);
            SetupAuxiliaryFileReads(new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase));

            CachedDataSource cachedDataSource = new(_connectorFactoryMock.Object, _cache, _loggerMock.Object);

            // Act
            IReadOnlyList<TableGroup> groups = await cachedDataSource.GetGroupingsCachedAsync(pxFile);

            // Assert
            Assert.That(groups, Is.Empty);
        }

        [Test]
        public async Task GetGroupingsCachedAsync_WhenGroupingFileExists_ReturnsGroupings()
        {
            // Arrange
            PxFileRef pxFile = PxFileRef.ValidateAndCreate("table1", _dbRef, ["level1", "level2"]);
            Dictionary<string, string> groupingNames = new(StringComparer.OrdinalIgnoreCase)
            {
                ["fi"] = "Grouping FI",
                ["sv"] = "Grouping SV",
                ["en"] = "Grouping EN"
            };

            Dictionary<string, string> aliasNames = new(StringComparer.OrdinalIgnoreCase)
            {
                ["fi"] = "Alias FI",
                ["sv"] = "Alias SV",
                ["en"] = "Alias EN"
            };

            string groupingsJson = JsonSerializer.Serialize(new
            {
                Code = "grouping-code",
                Name = groupingNames
            });

            Dictionary<string, byte[]> fileContents = new(StringComparer.OrdinalIgnoreCase)
            {
                ["groupings.json"] = Encoding.UTF8.GetBytes(groupingsJson),
                ["level1/level2/Alias_fi.txt"] = Encoding.UTF8.GetBytes(aliasNames["fi"]),
                ["level1/level2/Alias_sv.txt"] = Encoding.UTF8.GetBytes(aliasNames["sv"]),
                ["level1/level2/Alias_en.txt"] = Encoding.UTF8.GetBytes(aliasNames["en"])
            };

            SetupAuxiliaryFileReads(fileContents);

            CachedDataSource cachedDataSource = new(_connectorFactoryMock.Object, _cache, _loggerMock.Object);

            // Act
            IReadOnlyList<TableGroup> groups = await cachedDataSource.GetGroupingsCachedAsync(pxFile);

            // Assert
            MultilanguageString expectedAliases = new(aliasNames);
            MultilanguageString expectedGroupingName = new(groupingNames);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(groups, Has.Count.EqualTo(1));
                Assert.That(groups[0].Code, Is.EqualTo(pxFile.Hierarchy));
                Assert.That(groups[0].GroupingCode, Is.EqualTo("grouping-code"));
                Assert.That(groups[0].GroupingName, Is.EqualTo(expectedGroupingName));
                Assert.That(groups[0].Name, Is.EqualTo(expectedAliases));
                Assert.That(groups[0].Links, Is.Empty);
            }
        }

        private void SetupAuxiliaryFileReads(Dictionary<string, byte[]> fileContents)
        {
            _connectorMock.Setup(connector => connector.TryReadAuxiliaryFileAsync(It.IsAny<string>(), It.IsAny<string[]?>(), It.IsAny<CancellationToken>()))
                .Returns((string fileName, string[]? hierarchy, CancellationToken ct) =>
                {
                    string key = BuildAuxiliaryFileKey(fileName, hierarchy);
                    if (!fileContents.TryGetValue(key, out byte[]? contents))
                    {
                        throw new FileNotFoundException("Auxiliary file not found", key);
                    }
                    return Task.FromResult<Stream>(new MemoryStream(contents));
                });
        }

        private static string BuildAuxiliaryFileKey(string fileName, string[]? hierarchy)
        {
            if (hierarchy is null || hierarchy.Length == 0)
            {
                return fileName;
            }

            return string.Join('/', hierarchy) + "/" + fileName;
        }
    }
}
