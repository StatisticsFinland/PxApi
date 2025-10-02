using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Moq;
using Px.Utils.Models.Data;
using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Metadata;
using PxApi.Caching;
using PxApi.Configuration;
using PxApi.DataSources;
using PxApi.Models;
using PxApi.UnitTests.Models;
using PxApi.UnitTests.Utils;
using System.Collections.Immutable;
using System.Text;

namespace PxApi.UnitTests.Caching
{
    [TestFixture]
    internal class CachedDataBaseConnectorTests
    {
        [SetUp]
        public void SetUp()
        {
            Dictionary<string, string?> inMemorySettings = new()
            {
                {"RootUrl", "https://testurl.fi"}
            };

            foreach (KeyValuePair<string, string?> kvp in CreateDatabaseSettings(0, "PxApiUnitTestsDb"))
                inMemorySettings[kvp.Key] = kvp.Value;
            foreach (KeyValuePair<string, string?> kvp in CreateDatabaseSettings(1, "AnotherPxApiUnitTestsDb"))
                inMemorySettings[kvp.Key] = kvp.Value;

            IConfiguration _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            AppSettings.Load(_configuration);
        }

        private static PxFileRef BuildTestFileRef(string name, DataBaseRef dbRef)
        {
            return PxFileRef.CreateFromPath(Path.Combine("C:", "DbRoot", "folder", $"{name}.px"), dbRef);
        }

        #region GetDataBaseReference

        [Test]
        public void GetDataBaseReference_WithValidId_ReturnsDataBaseRef()
        {
            // Arrange
            Mock<IDataBaseConnectorFactory> mockFactory = new();
            DataBaseRef dataBase = DataBaseRef.Create("PxApiUnitTestsDb");
            mockFactory.Setup(mf => mf.GetAvailableDatabases()).Returns([dataBase]);
            CachedDataSource dataBaseConnector = new(mockFactory.Object, new DatabaseCache(new Mock<IMemoryCache>().Object));

            // Act
            DataBaseRef? result = dataBaseConnector.GetDataBaseReference("PxApiUnitTestsDb");

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result?.Id, Is.EqualTo(dataBase.Id));
            });
        }

        [Test]
        public void GetDataBaseReference_WithMissingId_ReturnsNull()
        {
            // Arrange
            Mock<IDataBaseConnectorFactory> mockFactory = new();
            mockFactory.Setup(mf => mf.GetAvailableDatabases()).Returns([]);
            CachedDataSource dataBaseConnector = new(mockFactory.Object, new DatabaseCache(new Mock<IMemoryCache>().Object));

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
            CachedDataSource dataBaseConnector = new(mockFactory.Object, new DatabaseCache(new Mock<IMemoryCache>().Object));

            // Act
            IReadOnlyCollection<DataBaseRef> result = dataBaseConnector.GetAllDataBaseReferences();

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Has.Count.EqualTo(3));
                Assert.That(result, Is.EquivalentTo(databases));
            });
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
            CachedDataSource connector = new(mockFactory.Object, dbCache);

            // Act
            ImmutableSortedDictionary<string, PxFileRef> result = await connector.GetFileListCachedAsync(dataBase);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result.Keys, Is.EquivalentTo(expected));
                Assert.That(result["file1"].Id, Is.EqualTo("file1"));
                Assert.That(result["file2"].Id, Is.EqualTo("file2"));
            });
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
            mockConnector.Setup(c => c.GetAllFilesAsync()).ReturnsAsync(fileNames);
            mockConnector.SetupGet(c => c.DataBase).Returns(dataBase);
            CachedDataSource connector = new(mockFactory.Object, dbCache);
            string[] expected = ["file1", "file2"];

            // Act
            ImmutableSortedDictionary<string, PxFileRef> result = await connector.GetFileListCachedAsync(dataBase);
            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result.Keys, Is.EquivalentTo(expected));
                Assert.That(result["file1"].Id, Is.EqualTo("file1"));
                Assert.That(result["file2"].Id, Is.EqualTo("file2"));
            });
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
            CachedDataSource connector = new(mockFactory.Object, dbCache);

            // Act
            PxFileRef? result = await connector.GetFileReferenceCachedAsync("file1", dataBase);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result?.Id, Is.EqualTo("file1"));
                Assert.That(result?.DataBase.Id, Is.EqualTo("PxApiUnitTestsDb"));
            });
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
            CachedDataSource connector = new(mockFactory.Object, dbCache);

            // Act & Assert
            Assert.That(await connector.GetFileReferenceCachedAsync("missingfile", dataBase), Is.Null);
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
            CachedDataSource connector = new(mockFactory.Object, dbCache);

            // Act
            IReadOnlyMatrixMetadata result = await connector.GetMetadataCachedAsync(fileRef);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.EqualTo(metadata));
            });
        }

        [Test]
        public async Task GetMetadataCachedAsync_MetadataNotInCache_FetchesAndReturnsMetadata()
        {
            // Arrange
            DataBaseRef dataBase = DataBaseRef.Create("PxApiUnitTestsDb");
            PxFileRef fileRef = PxFileRef.CreateFromPath(Path.Combine("C:", "foo", "file1.px"), dataBase);
            string pxContent = PxFixtures.MinimalPx.MINIMAL_UTF8_N;
            MemoryStream pxStream = new (Encoding.UTF8.GetBytes(pxContent));
            Mock<IDataBaseConnector> mockConnector = new();
            mockConnector.Setup(c => c.DataBase).Returns(dataBase);
            mockConnector.Setup(c => c.ReadPxFile(fileRef)).Returns(pxStream);
            mockConnector.Setup(c => c.GetLastWriteTimeAsync(fileRef)).ReturnsAsync(DateTime.UtcNow);
            Mock<IDataBaseConnectorFactory> mockFactory = new();
            mockFactory.Setup(f => f.GetConnector(dataBase)).Returns(mockConnector.Object);
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);
            CachedDataSource connector = new(mockFactory.Object, dbCache);
            string[] expectedLanguages = ["fi", "en"];

            // Act
            IReadOnlyMatrixMetadata result = await connector.GetMetadataCachedAsync(fileRef);

            // Assert
            Assert.Multiple(() =>
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
            });
        }

        #endregion

        #region GetSingleStringValueAsync

        [Test]
        public async Task GetSingleStringValueAsync_WithValidReferenceAndKey_ReturnsStringValue()
        {
            // Arrange
            string key = "LANGUAGE";
            DataBaseRef dbRef = DataBaseRef.Create("PxApiUnitTestsDb");
            PxFileRef fileRef = PxFileRef.CreateFromPath(Path.Combine("C:", "foo", "file1.px"), dbRef);
            string pxContent = PxFixtures.MinimalPx.MINIMAL_UTF8_N;
            MemoryStream pxStream = new (Encoding.UTF8.GetBytes(pxContent));
            Mock<IDataBaseConnector> mockConnector = new();
            mockConnector.Setup(c => c.DataBase).Returns(dbRef);
            mockConnector.Setup(c => c.ReadPxFile(fileRef)).Returns(pxStream);
            Mock<IDataBaseConnectorFactory> mockFactory = new();
            mockFactory.Setup(f => f.GetConnector(dbRef)).Returns(mockConnector.Object);
            CachedDataSource dbConnector = new(mockFactory.Object, new DatabaseCache(new MemoryCache(new MemoryCacheOptions())));

            // Act
            string result = await dbConnector.GetSingleStringValueAsync(key, fileRef);

            // Assert
            Assert.That(result, Is.EqualTo("\"fi\""));
        }

        [Test]
        public void GetSingleStringValueAsync_WithUnseekableStream_ThrowsInvalidOperationException()
        {
            // Arrange
            string key = "LANGUAGE";
            DataBaseRef dbRef = DataBaseRef.Create("PxApiUnitTestsDb");
            PxFileRef fileRef = PxFileRef.CreateFromPath(Path.Combine("C:", "foo", "file1.px"), dbRef);
            // Create a stream that cannot seek
            Stream unseekableStream = new UnseekableMemoryStream(Encoding.UTF8.GetBytes(PxFixtures.MinimalPx.MINIMAL_UTF8_N));
            Mock<IDataBaseConnector> mockConnector = new();
            mockConnector.Setup(c => c.DataBase).Returns(dbRef);
            mockConnector.Setup(c => c.ReadPxFile(fileRef)).Returns(unseekableStream);
            Mock<IDataBaseConnectorFactory> mockFactory = new();
            mockFactory.Setup(f => f.GetConnector(dbRef)).Returns(mockConnector.Object);
            CachedDataSource dbConnector = new(mockFactory.Object, new DatabaseCache(new MemoryCache(new MemoryCacheOptions())));

            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await dbConnector.GetSingleStringValueAsync(key, fileRef);
            });
        }

        [Test]
        public void GetSingleStringValueAsync_WithMissingKey_ThrowsInvalidOperationException()
        {
            // Arrange
            string key = "TEST";
            DataBaseRef dbRef = DataBaseRef.Create("PxApiUnitTestsDb");
            PxFileRef fileRef = PxFileRef.CreateFromPath(Path.Combine("C:", "foo", "file1.px"), dbRef);
            string pxContent = PxFixtures.MinimalPx.MINIMAL_UTF8_N;
            MemoryStream pxStream = new (Encoding.UTF8.GetBytes(pxContent));
            Mock<IDataBaseConnector> mockConnector = new();
            mockConnector.Setup(c => c.DataBase).Returns(dbRef);
            mockConnector.Setup(c => c.ReadPxFile(fileRef)).Returns(pxStream);
            Mock<IDataBaseConnectorFactory> mockFactory = new();
            mockFactory.Setup(f => f.GetConnector(dbRef)).Returns(mockConnector.Object);
            CachedDataSource dbConnector = new(mockFactory.Object, new DatabaseCache(new MemoryCache(new MemoryCacheOptions())));

            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await dbConnector.GetSingleStringValueAsync(key, fileRef);
            });
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
            CachedDataSource connector = new(mockFactory.Object, dbCache);

            // Act
            DoubleDataValue[] result = await connector.GetDataCachedAsync(pxFile, map);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Has.Length.EqualTo(1));
                Assert.That(result[0].UnsafeValue, Is.EqualTo(2));
            });
            mockConnector.Verify(c => c.ReadPxFile(It.IsAny<PxFileRef>()), Times.Never);
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
            CachedDataSource connector = new(mockFactory.Object, dbCache);

            // Act
            DoubleDataValue[] result = await connector.GetDataCachedAsync(pxFile, subsetMap);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Has.Length.EqualTo(1));
                Assert.That(result[0].UnsafeValue, Is.EqualTo(2)); // The value for 2025
            });
            mockConnector.Verify(c => c.ReadPxFile(It.IsAny<PxFileRef>()), Times.Never);
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
            mockConnector.Setup(c => c.ReadPxFile(pxFile))
                .Returns(() => new MemoryStream(Encoding.UTF8.GetBytes(PxFixtures.MinimalPx.MINIMAL_UTF8_N)));
            Mock<IDataBaseConnectorFactory> mockFactory = new();
            mockFactory.Setup(f => f.GetConnector(dataBase)).Returns(mockConnector.Object);
            CachedDataSource connector = new(mockFactory.Object, dbCache);

            // Act
            DoubleDataValue[] result = await connector.GetDataCachedAsync(pxFile, map);
            
            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Has.Length.EqualTo(1));
                Assert.That(result[0].UnsafeValue, Is.EqualTo(2));
            });
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
            mockConnector.Setup(c => c.GetAllFilesAsync()).ReturnsAsync([]);
            mockFactory.Setup(f => f.GetConnector(dataBase)).Returns(mockConnector.Object);
            CachedDataSource connector = new(mockFactory.Object, dbCache);

            // Act
            await connector.ClearDatabaseCacheAsync(dataBase);
            bool result = dbCache.TryGetFileList(dataBase, out Task<ImmutableSortedDictionary<string, PxFileRef>>? files);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(files, Is.Null);
            });
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
            CachedDataSource connector = new(mockFactory.Object, dbCache);
            
            // Pre-assert: last updated is present
            bool hasLastUpdated = dbCache.TryGetLastUpdated(file, out Task<DateTime>? beforeLastUpdated);
            Assert.Multiple(() =>
            {
                Assert.That(hasLastUpdated, Is.True);
                Assert.That(beforeLastUpdated, Is.Not.Null);
            });

            // Act
            connector.ClearTableCache(file);

            // Assert: last updated is removed
            bool afterHasLastUpdated = dbCache.TryGetLastUpdated(file, out Task<DateTime>? afterLastUpdated);
            Assert.Multiple(() =>
            {
                Assert.That(afterHasLastUpdated, Is.False);
                Assert.That(afterLastUpdated, Is.Null);
            });
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
            CachedDataSource connector = new(mockFactory.Object, dbCache);
            
            // Pre-assert: metadata is present
            bool hasMeta = dbCache.TryGetMetadata(file, out MetaCacheContainer? beforeMeta);
            Assert.Multiple(() =>
            {
                Assert.That(hasMeta, Is.True);
                Assert.That(beforeMeta, Is.SameAs(metaContainer));
            });

            // Act
            connector.ClearTableCache(file);

            // Assert: metadata is removed
            bool afterHasMeta = dbCache.TryGetMetadata(file, out MetaCacheContainer? afterMeta);
            Assert.Multiple(() =>
            {
                Assert.That(afterHasMeta, Is.False);
                Assert.That(afterMeta, Is.Null);
            });
        }

        #endregion

        private class UnseekableMemoryStream(byte[] buffer) : MemoryStream(buffer)
        {
            public override bool CanSeek => false;
        }

        private static Dictionary<string, string?> CreateDatabaseSettings(int index, string id)
        {
            return new Dictionary<string, string?>
            {
                {$"DataBases:{index}:Type", "Mounted"},
                {$"DataBases:{index}:Id", id},
                {$"DataBases:{index}:CacheConfig:TableList:SlidingExpirationSeconds", "900"},
                {$"DataBases:{index}:CacheConfig:TableList:AbsoluteExpirationSeconds", "900"},
                {$"DataBases:{index}:CacheConfig:Meta:SlidingExpirationSeconds", "900"},
                {$"DataBases:{index}:CacheConfig:Meta:AbsoluteExpirationSeconds", "900"},
                {$"DataBases:{index}:CacheConfig:Groupings:SlidingExpirationSeconds", "900"},
                {$"DataBases:{index}:CacheConfig:Groupings:AbsoluteExpirationSeconds", "900"},
                {$"DataBases:{index}:CacheConfig:Data:SlidingExpirationSeconds", "600"},
                {$"DataBases:{index}:CacheConfig:Data:AbsoluteExpirationSeconds", "600"},
                {$"DataBases:{index}:CacheConfig:Modifiedtime:SlidingExpirationSeconds", "60"},
                {$"DataBases:{index}:CacheConfig:Modifiedtime:AbsoluteExpirationSeconds", "60"},
                {$"DataBases:{index}:CacheConfig:MaxCacheSize", "1073741824"},
                {$"DataBases:{index}:Custom:RootPath", "datasource/root/"},
                {$"DataBases:{index}:Custom:ModifiedCheckIntervalMs", "1000"},
                {$"DataBases:{index}:Custom:FileListingCacheDurationMs", "10000"}
            };
        }
    }
}
