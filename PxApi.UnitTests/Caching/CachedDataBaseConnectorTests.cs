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
            CachedDataBaseConnector dataBaseConnector = new(mockFactory.Object, new DatabaseCache(new Mock<IMemoryCache>().Object));

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

        #region ClearFileListCache
        [Test]
        public void ClearFileListCache_RemovesCacheEntry()
        {
            // Arrange
            DataBaseRef dataBase = DataBaseRef.Create("PxApiUnitTestsDb");
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);

            ImmutableSortedDictionary<string, PxFileRef> files = ImmutableSortedDictionary<string, PxFileRef>.Empty
                .Add("file1", PxFileRef.Create("file1", dataBase))
                .Add("file2", PxFileRef.Create("file2", dataBase));
            dbCache.SetFileList(dataBase, Task.FromResult(files));

            Mock<IDataBaseConnectorFactory> mockFactory = new();
            CachedDataBaseConnector connector = new(mockFactory.Object, dbCache);

            // Pre-assert: file list is present
            bool hasFileList = dbCache.TryGetFileList(dataBase, out Task<ImmutableSortedDictionary<string, PxFileRef>>? beforeFiles);
            
            Assert.Multiple(() =>
            {
                Assert.That(hasFileList, Is.True);
                Assert.That(beforeFiles, Is.Not.Null);
            });

            // Act
            connector.ClearFileListCache(dataBase);

            // Assert: file list is removed
            bool afterHasFileList = dbCache.TryGetFileList(dataBase, out Task<ImmutableSortedDictionary<string, PxFileRef>>? afterFiles);
            Assert.Multiple(() =>
            {
                Assert.That(afterHasFileList, Is.False);
                Assert.That(afterFiles, Is.Null);
            });
        }

        #endregion

        #region ClearMetadataCacheAsync

        [Test]
        public async Task ClearMetadataCacheAsync_ClearsMetadataForAllFilesInDatabase()
        {
            // Arrange
            DataBaseRef dataBase = DataBaseRef.Create("PxApiUnitTestsDb");
            PxFileRef file1 = PxFileRef.Create("file1", dataBase);
            PxFileRef file2 = PxFileRef.Create("file2", dataBase);

            ImmutableSortedDictionary<string, PxFileRef> files = ImmutableSortedDictionary<string, PxFileRef>.Empty
                .Add("file1", file1)
                .Add("file2", file2);

            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);

            dbCache.SetFileList(dataBase, Task.FromResult(files));

            IReadOnlyMatrixMetadata metadata = await MatrixMetadataUtils.GetMetadataFromFixture(PxFixtures.MinimalPx.MINIMAL_UTF8_N);
            MetaCacheContainer metaContainer = new(Task.FromResult(metadata));
            dbCache.SetMetadata(file1, metaContainer);
            dbCache.SetMetadata(file2, metaContainer);

            Mock<IDataBaseConnectorFactory> mockFactory = new();
            CachedDataBaseConnector connector = new(mockFactory.Object, dbCache);

            // Pre-assert: metadata is present
            bool hasMeta1 = dbCache.TryGetMetadata(file1, out MetaCacheContainer? beforeMeta1);
            bool hasMeta2 = dbCache.TryGetMetadata(file2, out MetaCacheContainer? beforeMeta2);

            Assert.Multiple(() =>
            {
                Assert.That(hasMeta1, Is.True);
                Assert.That(beforeMeta1, Is.SameAs(metaContainer));
                Assert.That(hasMeta2, Is.True);
                Assert.That(beforeMeta2, Is.SameAs(metaContainer));
            });

            // Act
            await connector.ClearMetadataCacheAsync(dataBase);

            // Assert: metadata is removed
            bool afterHasMeta1 = dbCache.TryGetMetadata(file1, out MetaCacheContainer? afterMeta1);
            bool afterHasMeta2 = dbCache.TryGetMetadata(file2, out MetaCacheContainer? afterMeta2);

            Assert.Multiple(() =>
            {
                Assert.That(afterHasMeta1, Is.False);
                Assert.That(afterMeta1, Is.Null);
                Assert.That(afterHasMeta2, Is.False);
                Assert.That(afterMeta2, Is.Null);
            });
        }

        #endregion

        #region ClearDataCacheAsync

        [Test]
        public async Task ClearDataCacheAsync_ClearsDataCacheForAllFilesInDatabase()
        {
            // Arrange
            DataBaseRef dataBase = DataBaseRef.Create("PxApiUnitTestsDb");
            PxFileRef file1 = PxFileRef.Create("file1", dataBase);
            PxFileRef file2 = PxFileRef.Create("file2", dataBase);

            ImmutableSortedDictionary<string, PxFileRef> files = ImmutableSortedDictionary<string, PxFileRef>.Empty
                .Add("file1", file1)
                .Add("file2", file2);

            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);

            dbCache.SetFileList(dataBase, Task.FromResult(files));

            IReadOnlyMatrixMetadata metadata = await MatrixMetadataUtils.GetMetadataFromFixture(PxFixtures.MinimalPx.MINIMAL_UTF8_N);
            MetaCacheContainer metaContainer = new(Task.FromResult(metadata));
            dbCache.SetMetadata(file1, metaContainer);
            dbCache.SetMetadata(file2, metaContainer);

            // Store data for both files
            MatrixMap map1 = new([new DimensionMap("dim1", ["value1"]), new DimensionMap("dim2", ["2025"])]);
            MatrixMap map2 = new([new DimensionMap("dim1", ["value1"]), new DimensionMap("dim2", ["2024"])]);
            DoubleDataValue[] dataArray1 = [new DoubleDataValue(2, DataValueType.Exists)];
            DoubleDataValue[] dataArray2 = [new DoubleDataValue(1, DataValueType.Exists)];
            dbCache.SetData(file1, map1, Task.FromResult(dataArray1));
            dbCache.SetData(file2, map2, Task.FromResult(dataArray2));

            Mock<IDataBaseConnectorFactory> mockFactory = new();
            CachedDataBaseConnector connector = new(mockFactory.Object, dbCache);

            // Pre-assert: metadata and data are present
            bool hasMeta1 = dbCache.TryGetMetadata(file1, out MetaCacheContainer? beforeMeta1);
            bool hasMeta2 = dbCache.TryGetMetadata(file2, out MetaCacheContainer? beforeMeta2);
            bool hasData1 = dbCache.TryGetData(map1, out Task<DoubleDataValue[]>? beforeData1, out DateTime? _);
            bool hasData2 = dbCache.TryGetData(map2, out Task<DoubleDataValue[]>? beforeData2, out DateTime? _);

            Assert.Multiple(() =>
            {
                Assert.That(hasMeta1, Is.True);
                Assert.That(beforeMeta1, Is.SameAs(metaContainer));
                Assert.That(hasMeta2, Is.True);
                Assert.That(beforeMeta2, Is.SameAs(metaContainer));
                Assert.That(hasData1, Is.True);
                Assert.That(beforeData1, Is.Not.Null);
                Assert.That(hasData2, Is.True);
                Assert.That(beforeData2, Is.Not.Null);
            });

            // Act
            await connector.ClearDataCacheAsync(dataBase);

            // Assert: metadata is replaced, data is removed
            bool afterHasMeta1 = dbCache.TryGetMetadata(file1, out MetaCacheContainer? afterMeta1);
            bool afterHasMeta2 = dbCache.TryGetMetadata(file2, out MetaCacheContainer? afterMeta2);
            bool afterHasData1 = dbCache.TryGetData(map1, out Task<DoubleDataValue[]>? afterData1, out DateTime? _);
            bool afterHasData2 = dbCache.TryGetData(map2, out Task<DoubleDataValue[]>? afterData2, out DateTime? _);

            Assert.Multiple(() =>
            {
                Assert.That(afterHasMeta1, Is.True);
                Assert.That(afterMeta1, Is.Not.Null);
                Assert.That(afterMeta1, Is.Not.SameAs(metaContainer));
                Assert.That(afterHasMeta2, Is.True);
                Assert.That(afterMeta2, Is.Not.Null);
                Assert.That(afterMeta2, Is.Not.SameAs(metaContainer));
                Assert.That(afterHasData1, Is.False);
                Assert.That(afterData1, Is.Null);
                Assert.That(afterHasData2, Is.False);
                Assert.That(afterData2, Is.Null);
            });
        }

        #endregion

        #region ClearHierarchyCache

        [Test]
        public void ClearHierarchyCache_CallsDatabaseCacheClearHierarchyCache()
        {
            // Arrange
            DataBaseRef dataBase = DataBaseRef.Create("PxApiUnitTestsDb");
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);

            // Add a hierarchy to the cache
            Dictionary<string, List<string>> hierarchy = new()
            {
                { "group1", new List<string> { "file1", "file2" } },
                { "group2", new List<string> { "file3" } }
            };
            dbCache.SetHierarchy(dataBase, hierarchy);

            Mock<IDataBaseConnectorFactory> mockFactory = new();
            CachedDataBaseConnector connector = new(mockFactory.Object, dbCache);

            // Pre-assert: hierarchy is present
            bool hasHierarchy = dbCache.TryGetHierarchy(dataBase, out Dictionary<string, List<string>>? beforeHierarchy);
            Assert.Multiple(() =>
            {
                Assert.That(hasHierarchy, Is.True);
                Assert.That(beforeHierarchy, Is.Not.Null);
            });

            // Act
            connector.ClearHierarchyCache(dataBase);

            // Assert: hierarchy is removed
            bool afterHasHierarchy = dbCache.TryGetHierarchy(dataBase, out Dictionary<string, List<string>>? afterHierarchy);
            Assert.Multiple(() =>
            {
                Assert.That(afterHasHierarchy, Is.False);
                Assert.That(afterHierarchy, Is.Null);
            });
        }

        #endregion

        #region ClearAllCache

        [Test]
        public async Task ClearAllCache_ClearsDatabaseSpecificCaches()
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
            Mock<IDataBaseConnector> mockConnector = new();
            mockConnector.SetupGet(c => c.DataBase).Returns(dataBase);
            mockConnector.Setup(c => c.GetAllFilesAsync()).ReturnsAsync([]);
            mockFactory.Setup(f => f.GetConnector(dataBase)).Returns(mockConnector.Object);
            CachedDataBaseConnector connector = new(mockFactory.Object, dbCache);

            // Act
            await connector.ClearAllCache(dataBase);
            bool result = dbCache.TryGetFileList(dataBase, out Task<ImmutableSortedDictionary<string, PxFileRef>>? files);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(files, Is.Null);
            });
        }

        #endregion

        #region ClearAllCachesAsync
        [Test]
        public async Task ClearAllCachesAsync_ClearsAllCachesForAllDatabases()
        {
            // Arrange
            DataBaseRef db1 = DataBaseRef.Create("PxApiUnitTestsDb");
            DataBaseRef db2 = DataBaseRef.Create("AnotherPxApiUnitTestsDb");
            DataBaseRef[] databases = [db1, db2];

            Mock<IDataBaseConnectorFactory> mockFactory = new();
            mockFactory.Setup(f => f.GetAvailableDatabases()).Returns(databases);

            Mock<IDataBaseConnector> mockConnector1 = new();
            mockConnector1.SetupGet(c => c.DataBase).Returns(db1);
            mockConnector1.Setup(c => c.GetAllFilesAsync()).ReturnsAsync([]);
            Mock<IDataBaseConnector> mockConnector2 = new();
            mockConnector2.SetupGet(c => c.DataBase).Returns(db2);
            mockConnector2.Setup(c => c.GetAllFilesAsync()).ReturnsAsync([]);

            mockFactory.Setup(f => f.GetConnector(db1)).Returns(mockConnector1.Object);
            mockFactory.Setup(f => f.GetConnector(db2)).Returns(mockConnector2.Object);

            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);

            // Add file lists to cache for both databases
            dbCache.SetFileList(db1, Task.FromResult(
                ImmutableSortedDictionary<string, PxFileRef>.Empty
                    .Add("fileA", PxFileRef.Create("fileA", db1))
                    .Add("fileB", PxFileRef.Create("fileB", db1))));
            dbCache.SetHierarchy(db2, new Dictionary<string, List<string>>
            {
                { "group1", new List<string> { "fileX", "fileY" } },
                { "group2", new List<string> { "fileZ" } }
            });

            CachedDataBaseConnector connector = new(mockFactory.Object, dbCache);

            // Act
            await connector.ClearAllCachesAsync();

            // Assert
            bool result1 = dbCache.TryGetFileList(db1, out Task<ImmutableSortedDictionary<string, PxFileRef>>? files1);
            bool result2 = dbCache.TryGetHierarchy(db2, out Dictionary<string, List<string>>? hierarchy2);

            Assert.Multiple(() =>
            {
                Assert.That(result1, Is.False);
                Assert.That(files1, Is.Null);
                Assert.That(result2, Is.False);
                Assert.That(hierarchy2, Is.Null);
            });
        }

        #endregion

        #region ClearLastUpdatedCacheAsync

        [Test]
        public async Task ClearLastUpdatedCacheAsync_ClearsLastUpdatedCacheForDatabase()
        {
            // Arrange
            DataBaseRef dataBase = DataBaseRef.Create("PxApiUnitTestsDb");
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);
            PxFileRef pxRef = PxFileRef.Create("testfile", dataBase);
            Task<DateTime> lastUpdatedTask = Task.FromResult(DateTime.UtcNow);
            dbCache.SetFileList(dataBase, Task.FromResult(
                ImmutableSortedDictionary<string, PxFileRef>.Empty
                    .Add("testfile", pxRef)));
            dbCache.SetLastUpdated(pxRef, lastUpdatedTask);
            Mock<IDataBaseConnectorFactory> mockFactory = new();
            CachedDataBaseConnector connector = new(mockFactory.Object, dbCache);

            // Pre-assert: last updated is present
            bool hasLastUpdated = dbCache.TryGetLastUpdated(pxRef, out Task<DateTime>? beforeLastUpdated);
            Assert.Multiple(() =>
            {
                Assert.That(hasLastUpdated, Is.True);
                Assert.That(beforeLastUpdated, Is.Not.Null);
            });

            // Act
            await connector.ClearLastUpdatedCacheAsync(dataBase);

            // Assert: last updated is removed
            bool afterHasLastUpdated = dbCache.TryGetLastUpdated(pxRef, out Task<DateTime>? afterLastUpdated);
            Assert.Multiple(() =>
            {
                Assert.That(afterHasLastUpdated, Is.False);
                Assert.That(afterLastUpdated, Is.Null);
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
                {$"DataBases:{index}:CacheConfig:Data:SlidingExpirationSeconds", "600"},
                {$"DataBases:{index}:CacheConfig:Data:AbsoluteExpirationSeconds", "600"},
                {$"DataBases:{index}:CacheConfig:Modifiedtime:SlidingExpirationSeconds", "60"},
                {$"DataBases:{index}:CacheConfig:Modifiedtime:AbsoluteExpirationSeconds", "60"},
                {$"DataBases:{index}:CacheConfig:HierarchyConfig:SlidingExpirationSeconds", "1800"},
                {$"DataBases:{index}:CacheConfig:HierarchyConfig:AbsoluteExpirationSeconds", "1800"},
                {$"DataBases:{index}:CacheConfig:MaxCacheSize", "1073741824"},
                {$"DataBases:{index}:Custom:RootPath", "datasource/root/"},
                {$"DataBases:{index}:Custom:ModifiedCheckIntervalMs", "1000"},
                {$"DataBases:{index}:Custom:FileListingCacheDurationMs", "10000"}
            };
        }
    }
}
