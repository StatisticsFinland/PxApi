using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Px.Utils.Language;
using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Data;
using Px.Utils.Models.Metadata;
using PxApi.Caching;
using PxApi.DataSources;
using PxApi.Models;
using PxApi.UnitTests.Models;
using PxApi.UnitTests.Utils;
using System.Collections.Immutable;

namespace PxApi.UnitTests.Caching
{
    [TestFixture]
    internal class CachedDataBaseConnectorTests
    {
        [SetUp]
        public void SetUp()
        {
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                TestConfigFactory.MountedDb(0, "PxApiUnitTestsDb", "datasource/root/"),
                TestConfigFactory.MountedDb(1, "AnotherPxApiUnitTestsDb", "datasource/root/"),
                new Dictionary<string, string?>
                {
                    ["DataBases:0:CacheConfig:RevalidationIntervalMs"] = "500",
                    ["DataBases:1:CacheConfig:RevalidationIntervalMs"] = "500",
                    ["DataBases:0:Custom:ModifiedCheckIntervalMs"] = "1000",
                    ["DataBases:0:Custom:FileListingCacheDurationMs"] = "10000",
                    ["DataBases:1:Custom:ModifiedCheckIntervalMs"] = "1000",
                    ["DataBases:1:Custom:FileListingCacheDurationMs"] = "10000"
                }
            );
            TestConfigFactory.BuildAndLoad(configData);
        }

        private static PxFileRef BuildTestFileRef(string name, DataBaseRef dbRef)
        {
            return PxFileRef.CreateFromPath(Path.Combine("C:", "DbRoot", "folder", $"{name}.px"), dbRef);
        }

        private static Mock<ILogger<CachedDataSource>> CreateLoggerMock()
        {
            Mock<ILogger<CachedDataSource>> loggerMock = new(MockBehavior.Strict);
            loggerMock
                .Setup(l => l.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()))
                .Verifiable();

            return loggerMock;
        }

        private static void VerifyDebugLogged(Mock<ILogger<CachedDataSource>> loggerMock, string expectedMessage, Times times)
        {
            loggerMock.Verify(
                l => l.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((object v, Type _) => v.ToString() == expectedMessage),
                    It.IsAny<Exception?>(),
                    (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
                times);
        }

        #region GetDataBaseReference

        [Test]
        public void GetDataBaseReference_WithValidId_ReturnsDataBaseRef()
        {
            // Arrange
            Mock<IDataBaseConnectorFactory> mockFactory = new();
            DataBaseRef dataBase = DataBaseRef.Create("PxApiUnitTestsDb");
            mockFactory.Setup(mf => mf.GetAvailableDatabases()).Returns([dataBase]);
            Mock<ILogger<CachedDataSource>> loggerMock = CreateLoggerMock();
            CachedDataSource dataBaseConnector = new(mockFactory.Object, new DatabaseCache(new Mock<IMemoryCache>().Object), loggerMock.Object);

            // Act
            DataBaseRef? result = dataBaseConnector.GetDataBaseReference("PxApiUnitTestsDb");

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result?.Id, Is.EqualTo(dataBase.Id));
            }
        }

        [Test]
        public void GetDataBaseReference_WithMissingId_ReturnsNull()
        {
            // Arrange
            Mock<IDataBaseConnectorFactory> mockFactory = new();
            mockFactory.Setup(mf => mf.GetAvailableDatabases()).Returns([]);
            Mock<ILogger<CachedDataSource>> loggerMock = CreateLoggerMock();
            CachedDataSource dataBaseConnector = new(mockFactory.Object, new DatabaseCache(new Mock<IMemoryCache>().Object), loggerMock.Object);

            // Act
            DataBaseRef? result = dataBaseConnector.GetDataBaseReference("missingdatabase");

            // Assert
            Assert.That(result, Is.Null);
        }

        #endregion

        #region GetAllDataBaseReferences

        [Test]
        public void GetAllDataBaseReferences_ReturnsAllDatabases()
        {
            // Arrange
            Mock<IDataBaseConnectorFactory> mockFactory = new();
            DataBaseRef[] databases = [
                DataBaseRef.Create("db1"),
                DataBaseRef.Create("db2"),
                DataBaseRef.Create("db3")
            ];
            mockFactory.Setup(mf => mf.GetAvailableDatabases()).Returns(databases);
            Mock<ILogger<CachedDataSource>> loggerMock = CreateLoggerMock();
            CachedDataSource dataBaseConnector = new(mockFactory.Object, new DatabaseCache(new Mock<IMemoryCache>().Object), loggerMock.Object);

            // Act
            IReadOnlyCollection<DataBaseRef> result = dataBaseConnector.GetAllDataBaseReferences();

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Has.Count.EqualTo(3));
                Assert.That(result, Is.EquivalentTo(databases));
            }
            mockFactory.Verify(mf => mf.GetAvailableDatabases(), Times.Once);
        }

        #endregion

        #region GetFileListCachedAsync

        [Test]
        public async Task GetFileListCachedAsync_ReturnsFileList_WhenCacheHit()
        {
            // Arrange
            DataBaseRef dataBase = DataBaseRef.Create("PxApiUnitTestsDb");
            Mock<IDataBaseConnectorFactory> mockFactory = new();
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);
            dbCache.SetFileList(dataBase, Task.FromResult(
                ImmutableSortedDictionary<string, PxFileRef>.Empty
                    .Add("file1", BuildTestFileRef("file1", dataBase))
                    .Add("file2", BuildTestFileRef("file2", dataBase))));
            string[] expected = ["file1", "file2"];
            Mock<ILogger<CachedDataSource>> loggerMock = CreateLoggerMock();
            CachedDataSource connector = new(mockFactory.Object, dbCache, loggerMock.Object);

            // Act
            ImmutableSortedDictionary<string, PxFileRef> result = await connector.GetFileListCachedAsync(dataBase);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result.Keys, Is.EquivalentTo(expected));
                Assert.That(result["file1"].Id, Is.EqualTo("file1"));
                Assert.That(result["file2"].Id, Is.EqualTo("file2"));
            }
            VerifyDebugLogged(loggerMock, "File list cache hit.", Times.Once());
        }

        [Test]
        public async Task GetFileListCachedAsync_ReturnsFileList_WhenCacheMiss()
        {
            // Arrange
            DataBaseRef dataBase = DataBaseRef.Create("PxApiUnitTestsDb");
            string[] fileNames = ["file1.px", "file2.px"];
            Mock<IDataBaseConnectorFactory> mockFactory = new();
            Mock<IDataBaseConnector> mockConnector = new();
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);
            mockFactory.Setup(f => f.GetConnector(dataBase)).Returns(mockConnector.Object);
            mockConnector.Setup(c => c.GetAllFilesAsync(CancellationToken.None)).ReturnsAsync(fileNames);
            mockConnector.SetupGet(c => c.DataBase).Returns(dataBase);
            Mock<ILogger<CachedDataSource>> loggerMock = CreateLoggerMock();
            CachedDataSource connector = new(mockFactory.Object, dbCache, loggerMock.Object);
            string[] expected = ["file1", "file2"];

            // Act
            ImmutableSortedDictionary<string, PxFileRef> result = await connector.GetFileListCachedAsync(dataBase);
            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result.Keys, Is.EquivalentTo(expected));
                Assert.That(result["file1"].Id, Is.EqualTo("file1"));
                Assert.That(result["file2"].Id, Is.EqualTo("file2"));
            }
            VerifyDebugLogged(loggerMock, "File list cache miss. Reading from database.", Times.Once());
        }

        #endregion

        #region GetFileReferenceCachedAsync

        [Test]
        public async Task GetFileReferenceCachedAsync_FileExists_ReturnsFileRef()
        {
            // Arrange
            DataBaseRef dataBase = DataBaseRef.Create("PxApiUnitTestsDb");
            PxFileRef fileRef = PxFileRef.CreateFromPath(Path.Combine("C:", "foo", "file1.px"), dataBase);
            ImmutableSortedDictionary<string, PxFileRef> files = ImmutableSortedDictionary<string, PxFileRef>.Empty
                .Add("file1", fileRef);
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);
            dbCache.SetFileList(dataBase, Task.FromResult(files));
            Mock<IDataBaseConnectorFactory> mockFactory = new();
            Mock<ILogger<CachedDataSource>> loggerMock = CreateLoggerMock();
            CachedDataSource connector = new(mockFactory.Object, dbCache, loggerMock.Object);

            // Act
            PxFileRef? result = await connector.GetFileReferenceCachedAsync("file1", dataBase);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result?.Id, Is.EqualTo("file1"));
                Assert.That(result?.DataBase.Id, Is.EqualTo("PxApiUnitTestsDb"));
            }
            VerifyDebugLogged(loggerMock, "File list cache hit.", Times.Once());
        }

        [Test]
        public async Task GetFileReferenceCachedAsync_FileDoesNotExist_ReturnsNull()
        {
            // Arrange
            DataBaseRef dataBase = DataBaseRef.Create("PxApiUnitTestsDb");
            ImmutableSortedDictionary<string, PxFileRef> files = ImmutableSortedDictionary<string, PxFileRef>.Empty
                .Add("file1", PxFileRef.CreateFromPath(Path.Combine("C:", "foo", "file1.px"), dataBase));
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);
            dbCache.SetFileList(dataBase, Task.FromResult(files));
            Mock<IDataBaseConnectorFactory> mockFactory = new();
            Mock<ILogger<CachedDataSource>> loggerMock = CreateLoggerMock();
            CachedDataSource connector = new(mockFactory.Object, dbCache, loggerMock.Object);

            // Act & Assert
            Assert.That(await connector.GetFileReferenceCachedAsync("missingfile", dataBase), Is.Null);
            VerifyDebugLogged(loggerMock, "File list cache hit.", Times.Once());
        }

        #endregion

        #region GetMetadataCachedAsync

        [Test]
        public async Task GetMetadataCachedAsync_MetadataInCache_ReturnsMetadata()
        {
            // Arrange
            DataBaseRef dataBase = DataBaseRef.Create("PxApiUnitTestsDb");
            PxFileRef fileRef = PxFileRef.CreateFromPath(Path.Combine("C:", "foo", "file1.px"), dataBase);
            IReadOnlyMatrixMetadata metadata = await MatrixMetadataUtils.GetMetadataFromFixture(PxFixtures.MinimalPx.MINIMAL_UTF8_N);
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            MetaCacheContainer metaContainer = new(Task.FromResult(metadata));
            DatabaseCache dbCache = new(memoryCache);
            dbCache.SetMetadata(fileRef, metaContainer);
            Mock<IDataBaseConnectorFactory> mockFactory = new();
            Mock<IDataBaseConnector> mockConnector = new();
            mockFactory.Setup(f => f.GetConnector(dataBase)).Returns(mockConnector.Object);
            mockConnector.Setup(c => c.DataBase).Returns(dataBase);
            Mock<ILogger<CachedDataSource>> loggerMock = CreateLoggerMock();
            CachedDataSource connector = new(mockFactory.Object, dbCache, loggerMock.Object);

            // Act
            IReadOnlyMatrixMetadata result = await connector.GetMetadataCachedAsync(fileRef);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.EqualTo(metadata));
            }
            VerifyDebugLogged(loggerMock, "Metadata cache hit.", Times.Once());
        }

        [Test]
        public async Task GetMetadataCachedAsync_MetadataNotInCache_FetchesAndReturnsMetadata()
        {
            // Arrange
            DataBaseRef dataBase = DataBaseRef.Create("PxApiUnitTestsDb");
            PxFileRef fileRef = PxFileRef.CreateFromPath(Path.Combine("C:", "foo", "file1.px"), dataBase);
            Mock<IDataBaseConnector> mockConnector = new();
            mockConnector.Setup(c => c.DataBase).Returns(dataBase);
            IReadOnlyMatrixMetadata metadata = await MatrixMetadataUtils.GetMetadataFromFixture(PxFixtures.MinimalPx.MINIMAL_UTF8_N);
            mockConnector.Setup(c => c.ReadMetadataAsync(fileRef, CancellationToken.None)).ReturnsAsync(metadata);
            mockConnector.Setup(c => c.GetLastWriteTimeAsync(fileRef, CancellationToken.None)).ReturnsAsync(DateTime.UtcNow);
            Mock<IDataBaseConnectorFactory> mockFactory = new();
            mockFactory.Setup(f => f.GetConnector(dataBase)).Returns(mockConnector.Object);
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);
            Mock<ILogger<CachedDataSource>> loggerMock = CreateLoggerMock();
            CachedDataSource connector = new(mockFactory.Object, dbCache, loggerMock.Object);
            string[] expectedLanguages = ["fi", "en"];

            // Act
            IReadOnlyMatrixMetadata result = await connector.GetMetadataCachedAsync(fileRef);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result.DefaultLanguage, Is.EqualTo("fi"));
                Assert.That(result.AvailableLanguages, Is.EqualTo(expectedLanguages));
                Assert.That(result.Dimensions, Has.Count.EqualTo(2));
                Assert.That(result.Dimensions[0].Code, Is.EqualTo("dim1"));
                Assert.That(result.Dimensions[1].Code, Is.EqualTo("dim2"));
                Assert.That(result.Dimensions[0].Values, Has.Count.EqualTo(1));
                Assert.That(result.Dimensions[0].Values[0].Code, Is.EqualTo("value1"));
                Assert.That(result.Dimensions[1].Values, Has.Count.EqualTo(2));
                Assert.That(result.Dimensions[1].Values[0].Code, Is.EqualTo("2024"));
                Assert.That(result.Dimensions[1].Values[1].Code, Is.EqualTo("2025"));
            }
            VerifyDebugLogged(loggerMock, "Metadata cache miss. Reading from database.", Times.Once());
        }

        #endregion

        #region GetDataCachedAsync

        [Test]
        public async Task GetDataCachedAsync_DataInCache_ReturnsData()
        {
            // Arrange
            DataBaseRef dataBase = DataBaseRef.Create("PxApiUnitTestsDb");
            PxFileRef pxFile = PxFileRef.CreateFromPath(Path.Combine("C:", "foo", "testfile.px"), dataBase);
            MatrixMap map = new([
                new DimensionMap("dim1", ["value1"]),
                new DimensionMap("dim2", ["2025"])
            ]);
            DoubleDataValue[] expectedData = [new DoubleDataValue(2, DataValueType.Exists)];
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);
            IReadOnlyMatrixMetadata metadata = await MatrixMetadataUtils.GetMetadataFromFixture(PxFixtures.MinimalPx.MINIMAL_UTF8_N);
            MetaCacheContainer metaContainer = new(Task.FromResult(metadata));
            dbCache.SetMetadata(pxFile, metaContainer);
            dbCache.SetData(pxFile, map, Task.FromResult(expectedData));
            Mock<IDataBaseConnectorFactory> mockFactory = new();
            Mock<IDataBaseConnector> mockConnector = new();
            mockFactory.Setup(f => f.GetConnector(dataBase)).Returns(mockConnector.Object);
            Mock<ILogger<CachedDataSource>> loggerMock = CreateLoggerMock();
            CachedDataSource connector = new(mockFactory.Object, dbCache, loggerMock.Object);

            // Act
            DoubleDataValue[] result = await connector.GetDataCachedAsync(pxFile, map);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Has.Length.EqualTo(1));
                Assert.That(result[0].UnsafeValue, Is.EqualTo(2));
            }
            mockConnector.Verify(c => c.ReadDataAsync(It.IsAny<PxFileRef>(), It.IsAny<IMatrixMap>(), It.IsAny<IReadOnlyMatrixMetadata>(), It.IsAny<CancellationToken>()), Times.Never);
            VerifyDebugLogged(loggerMock, "Data cache exact hit.", Times.Once());
        }

        [Test]
        public async Task GetDataCachedAsync_SupersetDataInCache_ReturnsSubsetData()
        {
            // Arrange
            DataBaseRef dataBase = DataBaseRef.Create("PxApiUnitTestsDb");
            PxFileRef pxFile = PxFileRef.CreateFromPath(Path.Combine("C:", "foo", "testfile.px"), dataBase);
            MatrixMap subsetMap = new([
                new DimensionMap("dim1", ["value1"]),
                new DimensionMap("dim2", ["2025"])
            ]);
            MatrixMap supersetMap = new([
                new DimensionMap("dim1", ["value1"]),
                new DimensionMap("dim2", ["2024", "2025"])
            ]);
            DoubleDataValue[] supersetData = [
                new DoubleDataValue(1, DataValueType.Exists),  // value1, 2024
                new DoubleDataValue(2, DataValueType.Exists)   // value1, 2025
            ];
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);
            IReadOnlyMatrixMetadata metadata = await MatrixMetadataUtils.GetMetadataFromFixture(PxFixtures.MinimalPx.MINIMAL_UTF8_N);
            MetaCacheContainer metaContainer = new(Task.FromResult(metadata));
            dbCache.SetMetadata(pxFile, metaContainer);
            dbCache.SetData(pxFile, supersetMap, Task.FromResult(supersetData));
            Mock<IDataBaseConnectorFactory> mockFactory = new();
            Mock<IDataBaseConnector> mockConnector = new();
            mockFactory.Setup(f => f.GetConnector(dataBase)).Returns(mockConnector.Object);
            Mock<ILogger<CachedDataSource>> loggerMock = CreateLoggerMock();
            CachedDataSource connector = new(mockFactory.Object, dbCache, loggerMock.Object);

            // Act
            DoubleDataValue[] result = await connector.GetDataCachedAsync(pxFile, subsetMap);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Has.Length.EqualTo(1));
                Assert.That(result[0].UnsafeValue, Is.EqualTo(2)); // The value for 2025
            }
            mockConnector.Verify(c => c.ReadDataAsync(It.IsAny<PxFileRef>(), It.IsAny<IMatrixMap>(), It.IsAny<IReadOnlyMatrixMetadata>(), It.IsAny<CancellationToken>()), Times.Never);
            VerifyDebugLogged(loggerMock, "Data cache superset hit.", Times.Once());
        }

        [Test]
        public async Task GetDataCachedAsync_CacheMiss_ReadsFromSource()
        {
            // Arrange
            DataBaseRef dataBase = DataBaseRef.Create("PxApiUnitTestsDb");
            PxFileRef pxFile = PxFileRef.CreateFromPath(Path.Combine("C:", "foo", "testfile.px"), dataBase);
            MatrixMap map = new([
                new DimensionMap("dim1", ["value1"]),
                new DimensionMap("dim2", ["2025"])
            ]);
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);
            Mock<IDataBaseConnector> mockConnector = new();
            IReadOnlyMatrixMetadata metadata = await MatrixMetadataUtils.GetMetadataFromFixture(PxFixtures.MinimalPx.MINIMAL_UTF8_N);
            mockConnector.Setup(c => c.ReadMetadataAsync(pxFile, CancellationToken.None)).ReturnsAsync(metadata);
            mockConnector.Setup(c => c.ReadDataAsync(pxFile, map, metadata, CancellationToken.None)).ReturnsAsync([new DoubleDataValue(2, DataValueType.Exists)]);
            mockConnector.Setup(c => c.GetLastWriteTimeAsync(pxFile, CancellationToken.None)).ReturnsAsync(DateTime.UtcNow.AddMinutes(-5));
            Mock<IDataBaseConnectorFactory> mockFactory = new();
            mockFactory.Setup(f => f.GetConnector(dataBase)).Returns(mockConnector.Object);
            Mock<ILogger<CachedDataSource>> loggerMock = CreateLoggerMock();
            CachedDataSource connector = new(mockFactory.Object, dbCache, loggerMock.Object);

            // Act
            DoubleDataValue[] result = await connector.GetDataCachedAsync(pxFile, map);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Has.Length.EqualTo(1));
                Assert.That(result[0].UnsafeValue, Is.EqualTo(2));
            }
            VerifyDebugLogged(loggerMock, "Data cache miss. Reading from database.", Times.Once());
            VerifyDebugLogged(loggerMock, "Metadata cache miss. Reading from database.", Times.Once());
        }

        #endregion

        #region ClearDatabaseCacheAsync

        [Test]
        public async Task ClearDatabaseCacheAsync_ClearsDatabaseSpecificCaches()
        {
            // Arrange
            DataBaseRef dataBase = DataBaseRef.Create("PxApiUnitTestsDb");
            Mock<IDataBaseConnectorFactory> mockFactory = new();

            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);
            dbCache.SetFileList(dataBase, Task.FromResult(
                ImmutableSortedDictionary<string, PxFileRef>.Empty
                    .Add("file1", PxFileRef.CreateFromPath(Path.Combine("C:", "foo", "file1.px"), dataBase))
                    .Add("file2", PxFileRef.CreateFromPath(Path.Combine("C:", "foo", "file2.px"), dataBase))));
            Mock<IDataBaseConnector> mockConnector = new();
            mockConnector.SetupGet(c => c.DataBase).Returns(dataBase);
            mockConnector.Setup(c => c.GetAllFilesAsync(CancellationToken.None)).ReturnsAsync([]);
            mockFactory.Setup(f => f.GetConnector(dataBase)).Returns(mockConnector.Object);
            Mock<ILogger<CachedDataSource>> loggerMock = CreateLoggerMock();
            CachedDataSource connector = new(mockFactory.Object, dbCache, loggerMock.Object);

            // Act
            await connector.ClearDatabaseCacheAsync(dataBase);
            bool result = dbCache.TryGetFileList(dataBase, out Task<ImmutableSortedDictionary<string, PxFileRef>>? files);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.False);
                Assert.That(files, Is.Null);
            }
            VerifyDebugLogged(loggerMock, "File list cache hit.", Times.Once());
        }

        #endregion

        #region ClearTableCacheAsync

        [Test]
        public void ClearTableCacheAsync_ClearsTableSpecificCaches()
        {
            // Arrange
            DataBaseRef dataBase = DataBaseRef.Create("PxApiUnitTestsDb");
            PxFileRef file = BuildTestFileRef("testfile", dataBase);
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);

            // Add last updated timestamp to cache
            Task<DateTime> lastUpdatedTask = Task.FromResult(DateTime.UtcNow);
            dbCache.SetLastUpdated(file, lastUpdatedTask);

            Mock<IDataBaseConnectorFactory> mockFactory = new();
            Mock<ILogger<CachedDataSource>> loggerMock = CreateLoggerMock();
            CachedDataSource connector = new(mockFactory.Object, dbCache, loggerMock.Object);

            // Pre-assert: last updated is present
            bool hasLastUpdated = dbCache.TryGetLastUpdated(file, out Task<DateTime>? beforeLastUpdated);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(hasLastUpdated, Is.True);
                Assert.That(beforeLastUpdated, Is.Not.Null);
            }

            // Act
            connector.ClearTableCache(file);

            // Assert: last updated is removed
            bool afterHasLastUpdated = dbCache.TryGetLastUpdated(file, out Task<DateTime>? afterLastUpdated);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(afterHasLastUpdated, Is.False);
                Assert.That(afterLastUpdated, Is.Null);
            }
        }

        [Test]
        public async Task ClearTableCacheAsync_ClearsMetadataForFile()
        {
            // Arrange
            DataBaseRef dataBase = DataBaseRef.Create("PxApiUnitTestsDb");
            PxFileRef file = PxFileRef.CreateFromPath(Path.Combine("C:", "foo", "testfile.px"), dataBase);
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);

            // Add metadata to cache
            IReadOnlyMatrixMetadata metadata = await MatrixMetadataUtils.GetMetadataFromFixture(PxFixtures.MinimalPx.MINIMAL_UTF8_N);
            MetaCacheContainer metaContainer = new(Task.FromResult(metadata));
            dbCache.SetMetadata(file, metaContainer);

            Mock<IDataBaseConnectorFactory> mockFactory = new();
            Mock<ILogger<CachedDataSource>> loggerMock = CreateLoggerMock();
            CachedDataSource connector = new(mockFactory.Object, dbCache, loggerMock.Object);

            // Pre-assert: metadata is present
            bool hasMeta = dbCache.TryGetMetadata(file, out MetaCacheContainer? beforeMeta);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(hasMeta, Is.True);
                Assert.That(beforeMeta, Is.SameAs(metaContainer));
            }

            // Act
            connector.ClearTableCache(file);

            // Assert: metadata is removed
            bool afterHasMeta = dbCache.TryGetMetadata(file, out MetaCacheContainer? afterMeta);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(afterHasMeta, Is.False);
                Assert.That(afterMeta, Is.Null);
            }
        }

        #endregion

        #region Cache Revalidation Tests

        [Test]
        public async Task GetDataCachedAsync_RevalidationIntervalNull_DoesNotPerformRevalidation()
        {
            // Arrange (omit RevalidationIntervalMs entirely)
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                TestConfigFactory.MountedDb(0, "NoRevalidationDb", "datasource/root/"),
                new Dictionary<string, string?>
                {
                    ["DataBases:0:Custom:ModifiedCheckIntervalMs"] = "1000",
                    ["DataBases:0:Custom:FileListingCacheDurationMs"] = "10000"
                }
            );
            TestConfigFactory.BuildAndLoad(configData);

            DataBaseRef dataBase = DataBaseRef.Create("NoRevalidationDb");
            PxFileRef pxFile = PxFileRef.CreateFromPath(Path.Combine("C:", "foo", "testfile.px"), dataBase);
            MatrixMap map = new([
                new DimensionMap("dim1", ["value1"]),
                new DimensionMap("dim2", ["2025"])
            ]);
            DoubleDataValue[] expectedData = [new DoubleDataValue(2, DataValueType.Exists)];
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);
            IReadOnlyMatrixMetadata metadata = await MatrixMetadataUtils.GetMetadataFromFixture(PxFixtures.MinimalPx.MINIMAL_UTF8_N);
            MetaCacheContainer metaContainer = new(Task.FromResult(metadata));
            dbCache.SetMetadata(pxFile, metaContainer);
            dbCache.SetData(pxFile, map, Task.FromResult(expectedData));

            Mock<IDataBaseConnector> mockConnector = new();
            mockConnector.SetupGet(c => c.DataBase).Returns(dataBase);
            // This should never be called since revalidation is disabled
            mockConnector.Setup(c => c.GetLastWriteTimeAsync(It.IsAny<PxFileRef>(), CancellationToken.None))
                .ReturnsAsync(DateTime.UtcNow.AddDays(1)); // Future date to make cache invalid if checked

            Mock<IDataBaseConnectorFactory> mockFactory = new();
            mockFactory.Setup(f => f.GetConnector(dataBase)).Returns(mockConnector.Object);
            Mock<ILogger<CachedDataSource>> loggerMock = CreateLoggerMock();
            CachedDataSource connector = new(mockFactory.Object, dbCache, loggerMock.Object);

            // Act
            DoubleDataValue[] result = await connector.GetDataCachedAsync(pxFile, map);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Has.Length.EqualTo(1));
                Assert.That(result[0].UnsafeValue, Is.EqualTo(2));
            }

            // Verify that GetLastWriteTimeAsync was never called since revalidation is disabled
            mockConnector.Verify(c => c.GetLastWriteTimeAsync(It.IsAny<PxFileRef>(), CancellationToken.None), Times.Never);
            VerifyDebugLogged(loggerMock, "Data cache exact hit.", Times.Once());
        }

        [Test]
        public async Task GetDataCachedAsync_RevalidationIntervalZero_DoesNotPerformRevalidation()
        {
            // Arrange (set RevalidationIntervalMs explicitly to0)
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                TestConfigFactory.MountedDb(0, "ZeroRevalidationDb", "datasource/root/"),
                new Dictionary<string, string?>
                {
                    ["DataBases:0:CacheConfig:RevalidationIntervalMs"] = "0",
                    ["DataBases:0:Custom:ModifiedCheckIntervalMs"] = "1000",
                    ["DataBases:0:Custom:FileListingCacheDurationMs"] = "10000"
                }
            );
            TestConfigFactory.BuildAndLoad(configData);

            DataBaseRef dataBase = DataBaseRef.Create("ZeroRevalidationDb");
            PxFileRef pxFile = PxFileRef.CreateFromPath(Path.Combine("C:", "foo", "testfile.px"), dataBase);
            MatrixMap map = new([
                new DimensionMap("dim1", ["value1"]),
                new DimensionMap("dim2", ["2025"])
            ]);
            DoubleDataValue[] expectedData = [new DoubleDataValue(2, DataValueType.Exists)];
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);
            IReadOnlyMatrixMetadata metadata = await MatrixMetadataUtils.GetMetadataFromFixture(PxFixtures.MinimalPx.MINIMAL_UTF8_N);
            MetaCacheContainer metaContainer = new(Task.FromResult(metadata));
            dbCache.SetMetadata(pxFile, metaContainer);
            dbCache.SetData(pxFile, map, Task.FromResult(expectedData));

            Mock<IDataBaseConnector> mockConnector = new();
            mockConnector.SetupGet(c => c.DataBase).Returns(dataBase);
            // This should never be called since revalidation is disabled
            mockConnector.Setup(c => c.GetLastWriteTimeAsync(It.IsAny<PxFileRef>(), CancellationToken.None))
                .ReturnsAsync(DateTime.UtcNow.AddDays(1)); // Future date to make cache invalid if checked

            Mock<IDataBaseConnectorFactory> mockFactory = new();
            mockFactory.Setup(f => f.GetConnector(dataBase)).Returns(mockConnector.Object);
            Mock<ILogger<CachedDataSource>> loggerMock = CreateLoggerMock();
            CachedDataSource connector = new(mockFactory.Object, dbCache, loggerMock.Object);

            // Act
            DoubleDataValue[] result = await connector.GetDataCachedAsync(pxFile, map);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Has.Length.EqualTo(1));
                Assert.That(result[0].UnsafeValue, Is.EqualTo(2));
            }
            mockConnector.Verify(c => c.GetLastWriteTimeAsync(It.IsAny<PxFileRef>(), CancellationToken.None), Times.Never);
            VerifyDebugLogged(loggerMock, "Data cache exact hit.", Times.Once());
        }

        [Test]
        public async Task GetMetadataCachedAsync_RevalidationIntervalNull_DoesNotPerformRevalidation()
        {
            // Arrange (omit RevalidationIntervalMs entirely)
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                TestConfigFactory.MountedDb(0, "NoRevalidationMetaDb", "datasource/root/"),
                new Dictionary<string, string?>
                {
                    ["DataBases:0:Custom:ModifiedCheckIntervalMs"] = "1000",
                    ["DataBases:0:Custom:FileListingCacheDurationMs"] = "10000"
                }
            );
            TestConfigFactory.BuildAndLoad(configData);

            DataBaseRef dataBase = DataBaseRef.Create("NoRevalidationMetaDb");
            PxFileRef fileRef = PxFileRef.CreateFromPath(Path.Combine("C:", "foo", "file1.px"), dataBase);
            IReadOnlyMatrixMetadata metadata = await MatrixMetadataUtils.GetMetadataFromFixture(PxFixtures.MinimalPx.MINIMAL_UTF8_N);
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            MetaCacheContainer metaContainer = new(Task.FromResult(metadata));
            DatabaseCache dbCache = new(memoryCache);
            dbCache.SetMetadata(fileRef, metaContainer);

            Mock<IDataBaseConnector> mockConnector = new();
            mockConnector.SetupGet(c => c.DataBase).Returns(dataBase);
            // This should never be called since revalidation is disabled
            mockConnector.Setup(c => c.GetLastWriteTimeAsync(It.IsAny<PxFileRef>(), CancellationToken.None))
                .ReturnsAsync(DateTime.UtcNow.AddDays(1)); // Future date to make cache invalid if checked

            Mock<IDataBaseConnectorFactory> mockFactory = new();
            mockFactory.Setup(f => f.GetConnector(dataBase)).Returns(mockConnector.Object);
            Mock<ILogger<CachedDataSource>> loggerMock = CreateLoggerMock();
            CachedDataSource connector = new(mockFactory.Object, dbCache, loggerMock.Object);

            // Act
            IReadOnlyMatrixMetadata result = await connector.GetMetadataCachedAsync(fileRef);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.EqualTo(metadata));
            }
            VerifyDebugLogged(loggerMock, "Metadata cache hit.", Times.Once());
        }

        [Test]
        public async Task GetDataCachedAsync_RevalidationIntervalSet_PerformsRevalidation()
        {
            // Arrange - Use existing setup which includes RevalidationIntervalMs
            DataBaseRef dataBase = DataBaseRef.Create("PxApiUnitTestsDb"); // This has RevalidationIntervalMs = 500
            PxFileRef pxFile = PxFileRef.CreateFromPath(Path.Combine("C:", "foo", "testfile.px"), dataBase);
            MatrixMap map = new([
                new DimensionMap("dim1", ["value1"]),
                new DimensionMap("dim2", ["2025"])
            ]);
            DoubleDataValue[] expectedData = [new DoubleDataValue(2, DataValueType.Exists)];
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);
            IReadOnlyMatrixMetadata metadata = await MatrixMetadataUtils.GetMetadataFromFixture(PxFixtures.MinimalPx.MINIMAL_UTF8_N);
            MetaCacheContainer metaContainer = new(Task.FromResult(metadata));
            dbCache.SetMetadata(pxFile, metaContainer);
            dbCache.SetData(pxFile, map, Task.FromResult(expectedData));

            Mock<IDataBaseConnector> mockConnector = new();
            mockConnector.SetupGet(c => c.DataBase).Returns(dataBase);
            // Cache was created now, but file was modified in the past (cache is valid)
            mockConnector.Setup(c => c.GetLastWriteTimeAsync(pxFile, CancellationToken.None))
                .ReturnsAsync(DateTime.UtcNow.AddMinutes(-5));

            Mock<IDataBaseConnectorFactory> mockFactory = new();
            mockFactory.Setup(f => f.GetConnector(dataBase)).Returns(mockConnector.Object);
            Mock<ILogger<CachedDataSource>> loggerMock = CreateLoggerMock();
            CachedDataSource connector = new(mockFactory.Object, dbCache, loggerMock.Object);

            // Act
            DoubleDataValue[] result = await connector.GetDataCachedAsync(pxFile, map);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Has.Length.EqualTo(1));
                Assert.That(result[0].UnsafeValue, Is.EqualTo(2));
            }

            // Verify that GetLastWriteTimeAsync was called since revalidation is enabled
            mockConnector.Verify(c => c.GetLastWriteTimeAsync(pxFile, CancellationToken.None), Times.Once);
            VerifyDebugLogged(loggerMock, "Data cache exact hit.", Times.Once());
        }

        #endregion

        #region GetDatabaseNameAsync

        [Test]
        public async Task GetDatabaseNameAsync_WithCacheMiss_ReadsAndCachesName()
        {
            // Arrange
            DataBaseRef dbRef = DataBaseRef.Create("PxApiUnitTestsDb");
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);
            Mock<IDataBaseConnectorFactory> factoryMock = new();
            Mock<IDataBaseConnector> connectorMock = new();
            connectorMock.SetupGet(c => c.DataBase).Returns(dbRef);
            connectorMock.Setup(c => c.TryReadAuxiliaryFileAsync("Alias_fi.txt", CancellationToken.None)).Returns(BuildStream("Suomi"));
            connectorMock.Setup(c => c.TryReadAuxiliaryFileAsync("Alias_sv.txt", CancellationToken.None)).Returns(BuildStream("Finland"));
            connectorMock.Setup(c => c.TryReadAuxiliaryFileAsync("Alias_en.txt", CancellationToken.None)).Returns(BuildStream("Finland"));
            factoryMock.Setup(f => f.GetConnector(dbRef)).Returns(connectorMock.Object);
            Mock<ILogger<CachedDataSource>> loggerMock = CreateLoggerMock();
            CachedDataSource dataSource = new(factoryMock.Object, dbCache, loggerMock.Object);

            // Act
            MultilanguageString name = await dataSource.GetDatabaseNameAsync(dbRef, string.Empty);
            MultilanguageString cachedName = await dataSource.GetDatabaseNameAsync(dbRef, string.Empty);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(name, Is.Not.Null);
                Assert.That(cachedName, Is.SameAs(name));
            }
            connectorMock.Verify(c => c.TryReadAuxiliaryFileAsync(It.IsAny<string>(), CancellationToken.None), Times.Exactly(3));
        }

        [Test]
        public async Task GetDatabaseNameAsync_WithCacheHit_ReturnsCachedTask()
        {
            // Arrange
            DataBaseRef dbRef = DataBaseRef.Create("PxApiUnitTestsDb");
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);
            MultilanguageString expected = new(new Dictionary<string, string> { {"fi", "Suomi"} });
            dbCache.SetDatabaseName(dbRef, Task.FromResult(expected));
            Mock<IDataBaseConnectorFactory> factoryMock = new();
            Mock<ILogger<CachedDataSource>> loggerMock = CreateLoggerMock();
            CachedDataSource dataSource = new(factoryMock.Object, dbCache, loggerMock.Object);

            // Act
            MultilanguageString result = await dataSource.GetDatabaseNameAsync(dbRef, string.Empty);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.EqualTo(expected));
            }
        }

        [Test]
        public async Task ClearDatabaseCacheAsync_RemovesDatabaseName()
        {
            // Arrange
            DataBaseRef dbRef = DataBaseRef.Create("PxApiUnitTestsDb");
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);
            MultilanguageString expected = new(new Dictionary<string, string> { {"fi", "Suomi"} });
            dbCache.SetDatabaseName(dbRef, Task.FromResult(expected));
            Mock<IDataBaseConnectorFactory> factoryMock = new();
            Mock<IDataBaseConnector> connectorMock = new();
            connectorMock.SetupGet(c => c.DataBase).Returns(dbRef);
            connectorMock.Setup(c => c.GetAllFilesAsync(CancellationToken.None)).ReturnsAsync([]); // For ClearDatabaseCacheAsync
            factoryMock.Setup(f => f.GetConnector(dbRef)).Returns(connectorMock.Object);
            Mock<ILogger<CachedDataSource>> loggerMock = CreateLoggerMock();
            CachedDataSource dataSource = new(factoryMock.Object, dbCache, loggerMock.Object);

            // Pre-assert
            bool nameCached = dbCache.TryGetDatabaseName(dbRef, out Task<MultilanguageString>? beforeTask);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(nameCached, Is.True);
                Assert.That(beforeTask, Is.Not.Null);
            }

            // Act
            await dataSource.ClearDatabaseCacheAsync(dbRef);
            bool nameStillCached = dbCache.TryGetDatabaseName(dbRef, out Task<MultilanguageString>? afterTask);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(nameStillCached, Is.False);
                Assert.That(afterTask, Is.Null);
            }
            VerifyDebugLogged(loggerMock, "File list cache miss. Reading from database.", Times.Once());
        }

        #endregion

        #region EvictionOnFailedOrCanceledTasks

        [Test]
        public void SetFileList_FaultedTask_IsEvicted()
        {
            // Arrange
            DataBaseRef dbRef = DataBaseRef.Create("PxApiUnitTestsDb");
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);
            TaskCompletionSource<ImmutableSortedDictionary<string, PxFileRef>> tcs = new();
            dbCache.SetFileList(dbRef, tcs.Task);

            // Pre-assert: present in cache
            bool before = dbCache.TryGetFileList(dbRef, out Task<ImmutableSortedDictionary<string, PxFileRef>>? beforeTask);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(before, Is.True, "File list should be present in cache before faulting the task.");
                Assert.That(beforeTask, Is.Not.Null, "File list task should not be null before faulting the task.");
            }

            // Act: fault the task
            tcs.SetException(new InvalidOperationException("failure"));

            // Assert: evicted
            bool after = dbCache.TryGetFileList(dbRef, out Task<ImmutableSortedDictionary<string, PxFileRef>>? afterTask);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(after, Is.False, "File list should be evicted from cache after faulting the task.");
                Assert.That(afterTask, Is.Null, "File list task should be null after faulting the task.");
            }
        }

        [Test]
        public void SetDatabaseName_CanceledTask_IsEvicted()
        {
            // Arrange
            DataBaseRef dbRef = DataBaseRef.Create("PxApiUnitTestsDb");
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);
            TaskCompletionSource<MultilanguageString> tcs = new();
            dbCache.SetDatabaseName(dbRef, tcs.Task);

            // Pre-assert: present in cache
            bool before = dbCache.TryGetDatabaseName(dbRef, out Task<MultilanguageString>? beforeTask);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(before, Is.True, "Database name should be present in cache before canceling the task.");
                Assert.That(beforeTask, Is.Not.Null, "Database name task should not be null before canceling the task.");
            }

            // Act: cancel the task
            tcs.SetCanceled();

            // Assert: evicted
            bool after = dbCache.TryGetDatabaseName(dbRef, out Task<MultilanguageString>? afterTask);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(after, Is.False, "Database name should be evicted from cache after canceling the task.");
                Assert.That(afterTask, Is.Null, "Database name task should be null after canceling the task.");
            }
        }

        [Test]
        public void SetMetadata_FaultedTask_IsEvicted()
        {
            // Arrange
            DataBaseRef dbRef = DataBaseRef.Create("PxApiUnitTestsDb");
            PxFileRef fileRef = BuildTestFileRef("table1", dbRef);
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);
            TaskCompletionSource<IReadOnlyMatrixMetadata> metaTcs = new();
            MetaCacheContainer container = new(metaTcs.Task);
            dbCache.SetMetadata(fileRef, container);

            // Pre-assert: present in cache
            bool before = dbCache.TryGetMetadata(fileRef, out MetaCacheContainer? beforeMeta);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(before, Is.True, "Metadata should be present in cache before faulting the task.");
                Assert.That(beforeMeta, Is.Not.Null, "Metadata container should not be null before faulting the task.");
            }

            // Act: fault metadata task
            metaTcs.SetException(new InvalidOperationException("meta failure"));

            // Assert: evicted
            bool after = dbCache.TryGetMetadata(fileRef, out MetaCacheContainer? afterMeta);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(after, Is.False, "Metadata should be evicted from cache after faulting the task.");
                Assert.That(afterMeta, Is.Null, "Metadata container should be null after faulting the task.");
            }
        }

        [Test]
        public void SetMetadata_CanceledTask_IsEvicted()
        {
            // Arrange
            DataBaseRef dbRef = DataBaseRef.Create("PxApiUnitTestsDb");
            PxFileRef fileRef = BuildTestFileRef("table1", dbRef);
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);
            TaskCompletionSource<IReadOnlyMatrixMetadata> metaTcs = new();
            MetaCacheContainer container = new(metaTcs.Task);
            dbCache.SetMetadata(fileRef, container);

            // Pre-assert: present in cache
            bool before = dbCache.TryGetMetadata(fileRef, out MetaCacheContainer? beforeMeta);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(before, Is.True);
                Assert.That(beforeMeta, Is.Not.Null);
            }

            // Act: cancel metadata task
            metaTcs.SetCanceled();

            // Assert: evicted
            bool after = dbCache.TryGetMetadata(fileRef, out MetaCacheContainer? afterMeta);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(after, Is.False);
                Assert.That(afterMeta, Is.Null);
            }
        }

        [Test]
        public void SetData_FaultedTask_IsEvicted()
        {
            // Arrange
            DataBaseRef dbRef = DataBaseRef.Create("PxApiUnitTestsDb");
            PxFileRef fileRef = BuildTestFileRef("table1", dbRef);
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);
            // Need metadata in cache for SetData
            IReadOnlyMatrixMetadata metadata = MatrixMetadataUtils.GetMetadataFromFixture(PxFixtures.MinimalPx.MINIMAL_UTF8_N).GetAwaiter().GetResult();
            MetaCacheContainer metaContainer = new(Task.FromResult(metadata));
            dbCache.SetMetadata(fileRef, metaContainer);

            MatrixMap map = new([
                new DimensionMap("dim1", ["value1"]),
                new DimensionMap("dim2", ["2025"])
            ]);
            TaskCompletionSource<DoubleDataValue[]> dataTcs = new();
            dbCache.SetData(fileRef, map, dataTcs.Task);

            // Pre-assert: present in cache
            bool before = dbCache.TryGetData(map, out Task<DoubleDataValue[]>? beforeTask, out DateTime? beforeCached);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(before, Is.True);
                Assert.That(beforeTask, Is.Not.Null);
                Assert.That(beforeCached, Is.Not.Null);
            }

            // Act: fault data task
            dataTcs.SetException(new InvalidOperationException("data failure"));

            // Assert: evicted
            bool after = dbCache.TryGetData(map, out Task<DoubleDataValue[]>? afterTask, out DateTime? afterCached);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(after, Is.False);
                Assert.That(afterTask, Is.Null);
                Assert.That(afterCached, Is.Null);
            }
        }

        [Test]
        public void SetData_CanceledTask_IsEvicted()
        {
            // Arrange
            DataBaseRef dbRef = DataBaseRef.Create("PxApiUnitTestsDb");
            PxFileRef fileRef = BuildTestFileRef("table1", dbRef);
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);
            // Need metadata in cache for SetData
            IReadOnlyMatrixMetadata metadata = MatrixMetadataUtils.GetMetadataFromFixture(PxFixtures.MinimalPx.MINIMAL_UTF8_N).GetAwaiter().GetResult();
            MetaCacheContainer metaContainer = new(Task.FromResult(metadata));
            dbCache.SetMetadata(fileRef, metaContainer);

            MatrixMap map = new([
                new DimensionMap("dim1", ["value1"]),
                new DimensionMap("dim2", ["2025"])
            ]);
            TaskCompletionSource<DoubleDataValue[]> dataTcs = new();
            dbCache.SetData(fileRef, map, dataTcs.Task);

            // Pre-assert: present in cache
            bool before = dbCache.TryGetData(map, out Task<DoubleDataValue[]>? beforeTask, out DateTime? beforeCached);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(before, Is.True);
                Assert.That(beforeTask, Is.Not.Null);
                Assert.That(beforeCached, Is.Not.Null);
            }

            // Act: cancel data task
            dataTcs.SetCanceled();

            // Assert: evicted
            bool after = dbCache.TryGetData(map, out Task<DoubleDataValue[]>? afterTask, out DateTime? afterCached);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(after, Is.False);
                Assert.That(afterTask, Is.Null);
                Assert.That(afterCached, Is.Null);
            }
        }

        [Test]
        public async Task SetMetadata_Race_NewerEntryNotEvicted_ByPreviousFault()
        {
            // Arrange
            DataBaseRef dbRef = DataBaseRef.Create("PxApiUnitTestsDb");
            PxFileRef fileRef = BuildTestFileRef("table1", dbRef);
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);

            // First metadata task (will fault later)
            TaskCompletionSource<IReadOnlyMatrixMetadata> metaTcs1 = new();
            MetaCacheContainer container1 = new(metaTcs1.Task);
            dbCache.SetMetadata(fileRef, container1);

            // Ensure first is set
            bool firstPresent = dbCache.TryGetMetadata(fileRef, out MetaCacheContainer? first);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(firstPresent, Is.True);
                Assert.That(first, Is.SameAs(container1));
            }

            // Second metadata task (successful) set before first faults
            IReadOnlyMatrixMetadata metadata2 = await MatrixMetadataUtils.GetMetadataFromFixture(PxFixtures.MinimalPx.MINIMAL_UTF8_N);
            MetaCacheContainer container2 = new(Task.FromResult(metadata2));
            dbCache.SetMetadata(fileRef, container2);

            // Verify second replaced first
            bool secondPresent = dbCache.TryGetMetadata(fileRef, out MetaCacheContainer? current);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(secondPresent, Is.True);
                Assert.That(current, Is.SameAs(container2));
            }

            // Act: fault the first task; eviction continuation must not remove the newer entry
            metaTcs1.SetException(new InvalidOperationException("meta failure"));

            // Assert: still the newer entry present
            bool afterPresent = dbCache.TryGetMetadata(fileRef, out MetaCacheContainer? afterMeta);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(afterPresent, Is.True, "Newer metadata should remain in cache after previous task faults.");
                Assert.That(afterMeta, Is.SameAs(container2), "Newer metadata container should not be evicted by previous task's continuation.");
            }
        }

        #endregion

        private static Task<Stream> BuildStream(string content)
        {
            MemoryStream ms = new(System.Text.Encoding.UTF8.GetBytes(content + "\n"));
            return Task.FromResult<Stream>(ms);
        }
    }
}
