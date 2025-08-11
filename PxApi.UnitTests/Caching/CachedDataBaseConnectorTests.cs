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
                    {"RootUrl", "https://testurl.fi"},
                    {"DataBases:0:Type", "Mounted"},
                    {"DataBases:0:Id", "PxApiUnitTestsDb"},
                    {"DataBases:0:CacheConfig:TableList:SlidingExpirationSeconds", "900"},
                    {"DataBases:0:CacheConfig:TableList:AbsoluteExpirationSeconds", "900"},
                    {"DataBases:0:CacheConfig:Meta:SlidingExpirationSeconds", "900"}, // 15 minutes
                    {"DataBases:0:CacheConfig:Meta:AbsoluteExpirationSeconds", "900"}, // 15 minutes
                    {"DataBases:0:CacheConfig:Data:SlidingExpirationSeconds", "600"}, // 10 minutes
                    {"DataBases:0:CacheConfig:Data:AbsoluteExpirationSeconds", "600"}, // 10 minutes
                    {"DataBases:0:CacheConfig:Modifiedtime:SlidingExpirationSeconds", "60"},
                    {"DataBases:0:CacheConfig:Modifiedtime:AbsoluteExpirationSeconds", "60"},
                    {"DataBases:0:CacheConfig:HierarchyConfig:SlidingExpirationSeconds", "1800"}, // 30 minutes
                    {"DataBases:0:CacheConfig:HierarchyConfig:AbsoluteExpirationSeconds", "1800"}, // 30 minutes
                    {"DataBases:0:CacheConfig:MaxCacheSize", "1073741824"},
                    {"DataBases:0:Custom:RootPath", "datasource/root/"},
                    {"DataBases:0:Custom:ModifiedCheckIntervalMs", "1000"},
                    {"DataBases:0:Custom:FileListingCacheDurationMs", "10000"},
                };

            IConfiguration _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            AppSettings.Load(_configuration);
        }

        #region GetDataBaseReference

        [Test]
        public void GetDataBaseReference_WithValidId_ReturnsDataBaseRef()
        {
            // Arrange
            Mock<IDataBaseConnectorFactory> mockFactory = new();
            DataBaseRef dataBase = DataBaseRef.Create("PxApiUnitTestsDb");
            mockFactory.Setup(mf => mf.GetAvailableDatabases()).Returns([dataBase]);
            CachedDataBaseConnector dataBaseConnector = new(mockFactory.Object, new DatabaseCache(new Mock<IMemoryCache>().Object));

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
            CachedDataBaseConnector dataBaseConnector = new(mockFactory.Object, new DatabaseCache(new Mock<IMemoryCache>().Object));

            // Act
            DataBaseRef? result = dataBaseConnector.GetDataBaseReference("missingdatabase");

            // Assert
            Assert.That(result, Is.Null);
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
                    .Add("file1", PxFileRef.Create("file1", dataBase))
                    .Add("file2", PxFileRef.Create("file2", dataBase))));
            string[] expected = ["file1", "file2"];
            CachedDataBaseConnector connector = new(mockFactory.Object, dbCache);

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
            CachedDataBaseConnector connector = new(mockFactory.Object, dbCache);
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
            PxFileRef fileRef = PxFileRef.Create("file1", dataBase);
            ImmutableSortedDictionary<string, PxFileRef> files = ImmutableSortedDictionary<string, PxFileRef>.Empty
                .Add("file1", fileRef);
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);
            dbCache.SetFileList(dataBase, Task.FromResult(files));
            Mock<IDataBaseConnectorFactory> mockFactory = new();
            CachedDataBaseConnector connector = new(mockFactory.Object, dbCache);

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
        public void GetFileReferenceCachedAsync_FileDoesNotExist_ThrowsKeyNotFoundException()
        {
            // Arrange
            DataBaseRef dataBase = DataBaseRef.Create("PxApiUnitTestsDb");
            ImmutableSortedDictionary<string, PxFileRef> files = ImmutableSortedDictionary<string, PxFileRef>.Empty
                .Add("file1", PxFileRef.Create("file1", dataBase));
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);
            dbCache.SetFileList(dataBase, Task.FromResult(files));
            Mock<IDataBaseConnectorFactory> mockFactory = new();
            CachedDataBaseConnector connector = new(mockFactory.Object, dbCache);

            // Act & Assert
            Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            {
                await connector.GetFileReferenceCachedAsync("missingfile", dataBase);
            });
        }

        #endregion

        #region GetMetadataCachedAsync

        [Test]
        public async Task GetMetadataCachedAsync_MetadataInCache_ReturnsMetadata()
        {
            // Arrange
            DataBaseRef dataBase = DataBaseRef.Create("PxApiUnitTestsDb");
            PxFileRef fileRef = PxFileRef.Create("file1", dataBase);
            IReadOnlyMatrixMetadata metadata = await MatrixMetadataUtils.GetMetadataFromFixture(PxFixtures.MinimalPx.MINIMAL_UTF8_N);
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            MetaCacheContainer metaContainer = new(Task.FromResult(metadata));
            DatabaseCache dbCache = new(memoryCache);
            dbCache.SetMetadata(fileRef, metaContainer);
            Mock<IDataBaseConnectorFactory> mockFactory = new();
            Mock<IDataBaseConnector> mockConnector = new();
            mockFactory.Setup(f => f.GetConnector(dataBase)).Returns(mockConnector.Object);
            mockConnector.Setup(c => c.DataBase).Returns(dataBase);
            CachedDataBaseConnector connector = new(mockFactory.Object, dbCache);

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
            PxFileRef fileRef = PxFileRef.Create("file1", dataBase);
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
            CachedDataBaseConnector connector = new(mockFactory.Object, dbCache);
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
            PxFileRef fileRef = PxFileRef.Create("testfile", dbRef);
            string pxContent = PxFixtures.MinimalPx.MINIMAL_UTF8_N;
            MemoryStream pxStream = new (Encoding.UTF8.GetBytes(pxContent));
            Mock<IDataBaseConnector> mockConnector = new();
            mockConnector.Setup(c => c.DataBase).Returns(dbRef);
            mockConnector.Setup(c => c.ReadPxFile(fileRef)).Returns(pxStream);
            Mock<IDataBaseConnectorFactory> mockFactory = new();
            mockFactory.Setup(f => f.GetConnector(dbRef)).Returns(mockConnector.Object);
            CachedDataBaseConnector dbConnector = new(mockFactory.Object, new DatabaseCache(new MemoryCache(new MemoryCacheOptions())));

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
            PxFileRef fileRef = PxFileRef.Create("testfile", dbRef);
            // Create a stream that cannot seek
            Stream unseekableStream = new UnseekableMemoryStream(Encoding.UTF8.GetBytes(PxFixtures.MinimalPx.MINIMAL_UTF8_N));
            Mock<IDataBaseConnector> mockConnector = new();
            mockConnector.Setup(c => c.DataBase).Returns(dbRef);
            mockConnector.Setup(c => c.ReadPxFile(fileRef)).Returns(unseekableStream);
            Mock<IDataBaseConnectorFactory> mockFactory = new();
            mockFactory.Setup(f => f.GetConnector(dbRef)).Returns(mockConnector.Object);
            CachedDataBaseConnector dbConnector = new(mockFactory.Object, new DatabaseCache(new MemoryCache(new MemoryCacheOptions())));

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
            PxFileRef fileRef = PxFileRef.Create("testfile", dbRef);
            string pxContent = PxFixtures.MinimalPx.MINIMAL_UTF8_N;
            MemoryStream pxStream = new (Encoding.UTF8.GetBytes(pxContent));
            Mock<IDataBaseConnector> mockConnector = new();
            mockConnector.Setup(c => c.DataBase).Returns(dbRef);
            mockConnector.Setup(c => c.ReadPxFile(fileRef)).Returns(pxStream);
            Mock<IDataBaseConnectorFactory> mockFactory = new();
            mockFactory.Setup(f => f.GetConnector(dbRef)).Returns(mockConnector.Object);
            CachedDataBaseConnector dbConnector = new(mockFactory.Object, new DatabaseCache(new MemoryCache(new MemoryCacheOptions())));

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
            PxFileRef pxFile = PxFileRef.Create("testfile", dataBase);
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
            CachedDataBaseConnector connector = new(mockFactory.Object, dbCache);

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
            PxFileRef pxFile = PxFileRef.Create("testfile", dataBase);
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
            CachedDataBaseConnector connector = new(mockFactory.Object, dbCache);

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
            PxFileRef pxFile = PxFileRef.Create("testfile", dataBase);
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
            CachedDataBaseConnector connector = new(mockFactory.Object, dbCache);

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

        #region TryGetDataBaseHierarchy

        [Test]
        public void TryGetDataBaseHierarchy_WithCacheHit_ReturnsTrueWithHierarchy()
        {
            // Arrange
            DataBaseRef dataBase = DataBaseRef.Create("PxApiUnitTestsDb");
            Dictionary<string, List<string>> expectedHierarchy = new()
            {
                { "group1", new List<string> { "file1", "file2" } },
                { "group2", new List<string> { "file3", "file4" } }
            };
            
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache matrixCache = new(memoryCache);
            matrixCache.SetHierarchy(dataBase, expectedHierarchy);
            
            Mock<IDataBaseConnectorFactory> mockFactory = new();
            CachedDataBaseConnector connector = new(mockFactory.Object, matrixCache);
            string[] expectedGroup1 = ["file1", "file2"];
            string[] expectedGroup2 = ["file3", "file4"];

            // Act
            bool result = connector.TryGetDataBaseHierarchy(dataBase, out Dictionary<string, List<string>>? actualHierarchy);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(actualHierarchy, Is.Not.Null);
                Assert.That(actualHierarchy, Is.SameAs(expectedHierarchy));
                Assert.That(actualHierarchy!.Count, Is.EqualTo(2));
                Assert.That(actualHierarchy["group1"], Is.EquivalentTo(expectedGroup1));
                Assert.That(actualHierarchy["group2"], Is.EquivalentTo(expectedGroup2));
            });
        }

        [Test]
        public void TryGetDataBaseHierarchy_WithCacheMiss_ReturnsFalse()
        {
            // Arrange
            DataBaseRef dataBase = DataBaseRef.Create("PxApiUnitTestsDb");
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache matrixCache = new(memoryCache);
            
            Mock<IDataBaseConnectorFactory> mockFactory = new();
            CachedDataBaseConnector connector = new(mockFactory.Object, matrixCache);

            // Act
            bool result = connector.TryGetDataBaseHierarchy(dataBase, out Dictionary<string, List<string>>? hierarchy);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(hierarchy, Is.Null);
            });
        }

        #endregion

        #region SetDataBaseHierarchy

        [Test]
        public void SetDataBaseHierarchy_WithValidHierarchy_StoresHierarchyInCache()
        {
            // Arrange
            DataBaseRef dataBase = DataBaseRef.Create("PxApiUnitTestsDb");
            Dictionary<string, List<string>> hierarchyToStore = new()
            {
                { "group1", new List<string> { "file1", "file2" } },
                { "group2", new List<string> { "file3", "file4" } }
            };
            
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache matrixCache = new(memoryCache);
            
            Mock<IDataBaseConnectorFactory> mockFactory = new();
            CachedDataBaseConnector connector = new(mockFactory.Object, matrixCache);

            string[] expectedGroup1 = ["file1", "file2"];
            string[] expectedGroup2 = ["file3", "file4"];

            // Act
            connector.SetDataBaseHierarchy(dataBase, hierarchyToStore);

            // Assert
            bool result = matrixCache.TryGetHierarchy(dataBase, out Dictionary<string, List<string>>? retrievedHierarchy);
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(retrievedHierarchy, Is.Not.Null);
                Assert.That(retrievedHierarchy, Is.SameAs(hierarchyToStore));
                Assert.That(retrievedHierarchy!.Count, Is.EqualTo(2));
                Assert.That(retrievedHierarchy["group1"], Is.EquivalentTo(expectedGroup1));
                Assert.That(retrievedHierarchy["group2"], Is.EquivalentTo(expectedGroup2));
            });
        }

        #endregion

        private class UnseekableMemoryStream(byte[] buffer) : MemoryStream(buffer)
        {
            public override bool CanSeek => false;
        }
    }
}
