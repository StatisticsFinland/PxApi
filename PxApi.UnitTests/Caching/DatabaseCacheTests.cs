using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Moq;
using Px.Utils.Models.Data;
using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Metadata;
using PxApi.Caching;
using PxApi.Configuration;
using PxApi.Models;
using PxApi.UnitTests.Models;
using PxApi.UnitTests.Utils;
using System.Collections.Immutable;

namespace PxApi.UnitTests.Caching
{
    [TestFixture]
    internal class DatabaseCacheTests
    {
        private const string FILE_LIST_SEED_VARNAME = "FILE_LIST_SEED";
        private const string LAST_UPDATED_SEED_VARNAME = "LAST_UPDATED_SEED";
        private const string DATA_SEED_VARNAME = "DATA_SEED";
        private const string META_SEED_VARNAME = "META_SEED";

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
                    {"DataBases:0:CacheConfig:Groupings:SlidingExpirationSeconds", "900"},
                    {"DataBases:0:CacheConfig:Groupings:AbsoluteExpirationSeconds", "900"},
                    {"DataBases:0:CacheConfig:Data:SlidingExpirationSeconds", "600"}, // 10 minutes
                    {"DataBases:0:CacheConfig:Data:AbsoluteExpirationSeconds", "600"}, // 10 minutes
                    {"DataBases:0:CacheConfig:Modifiedtime:SlidingExpirationSeconds", "60"},
                    {"DataBases:0:CacheConfig:Modifiedtime:AbsoluteExpirationSeconds", "60"},
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

        #region TryGetFileList

        [Test]
        public void TryGetFileList_WithCacheHit_ReturnsTrueWithFileList()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("PxApiUnitTestsDb");
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            Task<ImmutableSortedDictionary<string, PxFileRef>> expectedFiles = Task.FromResult(
                ImmutableSortedDictionary<string, PxFileRef>.Empty
                    .Add("file1", PxFileRef.Create("file1", database))
                    .Add("file2", PxFileRef.Create("file2", database)));
            string fileListSeed = GetSeedForKeyword(FILE_LIST_SEED_VARNAME);
            memoryCache.Set(HashCode.Combine(fileListSeed, database), expectedFiles);
            DatabaseCache dbCache = new(memoryCache);

            // Act
            bool result = dbCache.TryGetFileList(database, out Task<ImmutableSortedDictionary<string, PxFileRef>>? actualFiles);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(actualFiles, Is.SameAs(expectedFiles));
            });
        }

        [Test]
        public void TryGetFileList_WithCacheMiss_ReturnsFalse()
        {
            // Arrange
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);
            DataBaseRef database = DataBaseRef.Create("PxApiUnitTestsDb");

            // Act
            bool result = dbCache.TryGetFileList(database, out Task<ImmutableSortedDictionary<string, PxFileRef>>? actualFiles);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(actualFiles, Is.Null);
            });
        }

        #endregion

        #region SetFileList

        [Test]
        public void SetFileList_WithDataBaseAndFiles_SetsCacheWithExpectedValues()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("PxApiUnitTestsDb");
            Task<ImmutableSortedDictionary<string, PxFileRef>> files = Task.FromResult(
                ImmutableSortedDictionary<string, PxFileRef>.Empty
                    .Add("file1", PxFileRef.Create("file1", database))
                    .Add("file2", PxFileRef.Create("file2", database)));
            
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);
            string fileListSeed = GetSeedForKeyword(FILE_LIST_SEED_VARNAME);

            // Act
            dbCache.SetFileList(database, files);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(memoryCache.TryGetValue(
                    HashCode.Combine(fileListSeed, database),
                    out Task<ImmutableSortedDictionary<string, PxFileRef>>? cachedFiles),
                    Is.True);
                Assert.That(cachedFiles, Is.SameAs(files));
            });
        }

        #endregion

        #region TryGetLastUpdated

        [Test]
        public void TryGetLastUpdated_WithCacheHit_ReturnsTruewithLastUpdated()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("PxApiUnitTestsDb");
            PxFileRef fileRef = PxFileRef.Create("file1", database);
            Task<DateTime> expectedLastUpdated = Task.FromResult(DateTime.UtcNow);
            
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            string lastUpdatedSeed = GetSeedForKeyword(LAST_UPDATED_SEED_VARNAME);
            memoryCache.Set(HashCode.Combine(lastUpdatedSeed, fileRef), expectedLastUpdated);
            DatabaseCache dbCache = new(memoryCache);

            // Act
            bool result = dbCache.TryGetLastUpdated(fileRef, out Task<DateTime>? actualLastUpdated);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(actualLastUpdated, Is.SameAs(expectedLastUpdated));
            });
        }

        [Test]
        public void TryGetLastUpdated_WithCacheMiss_ReturnsFalse()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("PxApiUnitTestsDb");
            PxFileRef fileRef = PxFileRef.Create("file1", database);
            
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);

            // Act
            bool result = dbCache.TryGetLastUpdated(fileRef, out Task<DateTime>? actualLastUpdated);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(actualLastUpdated, Is.Null);
            });
        }

        #endregion

        #region SetLastUpdated

        [Test]
        public void SetLastUpdated_WithFileRefAndTimeStamp_SetsCacheWithExpectedValues()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("PxApiUnitTestsDb");
            PxFileRef fileRef = PxFileRef.Create("file1", database);
            Task<DateTime> lastUpdated = Task.FromResult(DateTime.UtcNow);
            string lastUpdatedSeed = GetSeedForKeyword(LAST_UPDATED_SEED_VARNAME);
            
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);
                
            // Act
            dbCache.SetLastUpdated(fileRef, lastUpdated);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(memoryCache.TryGetValue(HashCode.Combine(lastUpdatedSeed, fileRef), out Task<DateTime>? cachedLastUpdated), Is.True);
                Assert.That(cachedLastUpdated, Is.SameAs(lastUpdated));
            });
        }

        #endregion

        #region TryGetMetadata

        [Test]
        public void TryGetMetadata_WithCacheHit_ReturnsTrueWithMetadataContainer()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("PxApiUnitTestsDb");
            PxFileRef fileRef = PxFileRef.Create("file1", database);
            Mock<IReadOnlyMatrixMetadata> mockMetadata = new();
            MetaCacheContainer expectedMetaContainer = new(Task.FromResult(mockMetadata.Object));
            
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            string metaSeed = GetSeedForKeyword(META_SEED_VARNAME);
            int metaKey = HashCode.Combine(metaSeed, fileRef);
            memoryCache.Set(metaKey, expectedMetaContainer);
            
            DatabaseCache dbCache = new(memoryCache);

            // Act
            bool result = dbCache.TryGetMetadata(fileRef, out MetaCacheContainer? actualMetaContainer);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(actualMetaContainer, Is.SameAs(expectedMetaContainer));
            });
        }

        [Test]
        public void TryGetMetadata_WithCacheMiss_ReturnsFalse()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("PxApiUnitTestsDb");
            PxFileRef fileRef = PxFileRef.Create("file1", database);
            
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);

            // Act
            bool result = dbCache.TryGetMetadata(fileRef, out MetaCacheContainer? actualMetaContainer);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(actualMetaContainer, Is.Null);
            });
        }

        #endregion

        #region SetMetadata

        [Test]
        public void SetMetadata_WithPxFileRefAndMetadataContainer_SetsCacheWithExpectedValues()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("PxApiUnitTestsDb");
            PxFileRef fileRef = PxFileRef.Create("file1", database);
            Mock<IReadOnlyMatrixMetadata> mockMetadata = new();
            MetaCacheContainer metaContainer = new(Task.FromResult(mockMetadata.Object));
            
            MemoryCache cache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(cache);
            string metaSeed = GetSeedForKeyword(META_SEED_VARNAME);
            int cacheKey = HashCode.Combine(metaSeed, fileRef);
            
            // Act
            dbCache.SetMetadata(fileRef, metaContainer);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(cache.TryGetValue(cacheKey, out MetaCacheContainer? cachedMetaContainer), Is.True);
                Assert.That(cachedMetaContainer, Is.SameAs(metaContainer));
            });
        }

        #endregion

        #region TryRemoveMeta

        [Test]
        public void TryRemoveMeta_WithCacheHit_RemovesMetaFromCache()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("PxApiUnitTestsDb");
            PxFileRef fileRef = PxFileRef.Create("file1", database);
            Mock<IReadOnlyMatrixMetadata> mockMetadata = new();
            MetaCacheContainer metaContainer = new(Task.FromResult(mockMetadata.Object));
            
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            string metaSeed = GetSeedForKeyword(META_SEED_VARNAME);
            int cacheKey = HashCode.Combine(metaSeed, fileRef);
            memoryCache.Set(cacheKey, metaContainer);
            
            DatabaseCache dbCache = new(memoryCache);

            // Act
            dbCache.TryRemoveMeta(fileRef);

            // Assert
            Assert.That(memoryCache.TryGetValue(cacheKey, out _), Is.False);
        }

        [Test]
        public void TryRemoveMeta_WithCacheMiss_DoesNotThrow()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("PxApiUnitTestsDb");
            PxFileRef fileRef = PxFileRef.Create("file1", database);
            
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);

            // Act & Assert
            Assert.Multiple(() =>
            {  
                Assert.That(memoryCache.Keys.Count, Is.EqualTo(0));
                Assert.That(dbCache.TryGetMetadata(fileRef, out _), Is.False);
                Assert.DoesNotThrow(() => dbCache.TryRemoveMeta(fileRef));
            });
        }

        #endregion

        #region TryGetData

        [Test]
        public void TryGetData_WithCacheHit_ReturnsTrueWithDataContainer()
        {
            // Arrange
            MatrixMap map = new([
                new DimensionMap("dim1", ["value1"]),
                new DimensionMap("dim2", ["2025"])
            ]);
            DoubleDataValue[] expectedData = [new DoubleDataValue(2, DataValueType.Exists)];
            Task<DoubleDataValue[]> expectedDataTask = Task.FromResult(expectedData);
            DataCacheContainer<DoubleDataValue> dataContainer = new(map, expectedDataTask);
            
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            string dataSeed = GetSeedForKeyword(DATA_SEED_VARNAME);
            int mapHash = GetMapHash(map);
            memoryCache.Set(HashCode.Combine(dataSeed, mapHash), dataContainer);
            
            DatabaseCache dbCache = new(memoryCache);

            // Act
            bool result = dbCache.TryGetData(map, out Task<DoubleDataValue[]>? actualData, out DateTime? actualCached);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(actualData, Is.SameAs(expectedDataTask));
                Assert.That(actualCached, Is.Not.Null);
            });
        }

        [Test]
        public void TryGetData_WithCacheMiss_ReturnsFalse()
        {
            // Arrange
            MatrixMap map = new([
                new DimensionMap("dim1", ["value1"]),
                new DimensionMap("dim2", ["2025"])
            ]);
            
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);

            // Act
            bool result = dbCache.TryGetData(map, out Task<DoubleDataValue[]>? actualData, out DateTime? actualCached);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(actualData, Is.Null);
                Assert.That(actualCached, Is.Null);
            });
        }

        #endregion

        #region TryGetDataSuperset

        [Test]
        public void TryGetDataSuperSet_WithCacheMiss_ReturnsFalse()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("PxApiUnitTestsDb");
            PxFileRef fileRef = PxFileRef.Create("file1", database);
            MatrixMap map = new([
                new DimensionMap("dim1", ["value1"]),
                new DimensionMap("dim2", ["2025"])
            ]);
            
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);

            // Act
            bool result = dbCache.TryGetDataSuperset(fileRef, map, out IMatrixMap? actualSupersetMap, 
                out Task<DoubleDataValue[]>? actualData, out DateTime? actualCached);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(actualSupersetMap, Is.Null);
                Assert.That(actualData, Is.Null);
                Assert.That(actualCached, Is.Null);
            });
        }

        [Test]
        public void TryGetDataSuperSet_WithCacheHitWithoutSuperMap_ReturnsFalse()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("PxApiUnitTestsDb");
            PxFileRef fileRef = PxFileRef.Create("file1", database);
            MatrixMap map = new([
                new DimensionMap("dim1", ["value1"]),
                new DimensionMap("dim2", ["2025"])
            ]);
            
            Mock<IReadOnlyMatrixMetadata> mockMetadata = new();
            MetaCacheContainer metaContainer = new(Task.FromResult(mockMetadata.Object));
            
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            string metaSeed = GetSeedForKeyword(META_SEED_VARNAME);
            memoryCache.Set(HashCode.Combine(metaSeed, fileRef), metaContainer);
            DatabaseCache dbCache = new(memoryCache);

            // Act
            bool result = dbCache.TryGetDataSuperset(fileRef, map, out IMatrixMap? actualSupersetMap, 
                out Task<DoubleDataValue[]>? actualData, out DateTime? actualCached);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(actualSupersetMap, Is.Null);
                Assert.That(actualData, Is.Null);
                Assert.That(actualCached, Is.Null);
            });
        }

        [Test]
        public void TryGetDataSuperSet_WithCacheHitWithSuperMapWithContainerMiss_ReturnsFalse()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("PxApiUnitTestsDb");
            PxFileRef fileRef = PxFileRef.Create("file1", database);
            MatrixMap map = new([
                new DimensionMap("dim1", ["value1"]),
                new DimensionMap("dim2", ["2025"])
            ]);
            MatrixMap superMap = new([
                new DimensionMap("dim1", ["value1"]),
                new DimensionMap("dim2", ["2024", "2025"])
            ]);
            
            Mock<IReadOnlyMatrixMetadata> mockMetadata = new();
            
            MetaCacheContainer metaContainer = new(Task.FromResult(mockMetadata.Object));
            DataCacheContainer<DoubleDataValue> dataContainer = new(superMap, Task.FromResult<DoubleDataValue[]>([
                new DoubleDataValue(1, DataValueType.Exists),
                new DoubleDataValue(2, DataValueType.Exists)
            ]));
            metaContainer.AddDataContainer(dataContainer);
            
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            string metaSeed = GetSeedForKeyword(META_SEED_VARNAME);
            memoryCache.Set(HashCode.Combine(metaSeed, fileRef), metaContainer);
            DatabaseCache dbCache = new(memoryCache);

            // Act
            bool result = dbCache.TryGetDataSuperset(fileRef, map, out IMatrixMap? actualSupersetMap, 
                out Task<DoubleDataValue[]>? actualData, out DateTime? actualCached);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(actualSupersetMap, Is.Null);
                Assert.That(actualData, Is.Null);
                Assert.That(actualCached, Is.Null);
            });
        }

        [Test]
        public void TryGetDataSuperSet_WithCachedSuperSetMap_ReturnsTrueWithSupersetDataContainer()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("PxApiUnitTestsDb");
            PxFileRef fileRef = PxFileRef.Create("file1", database);
            MatrixMap map = new([
                new DimensionMap("dim1", ["value1"]),
                new DimensionMap("dim2", ["2025"])
            ]);
            MatrixMap superMap = new([
                new DimensionMap("dim1", ["value1"]),
                new DimensionMap("dim2", ["2024", "2025"])
            ]);
            
            Mock<IReadOnlyMatrixMetadata> mockMetadata = new();
            DoubleDataValue[] expectedData = [
                new DoubleDataValue(1, DataValueType.Exists),
                new DoubleDataValue(2, DataValueType.Exists)
            ];
            Task<DoubleDataValue[]> expectedDataTask = Task.FromResult(expectedData);
            DataCacheContainer<DoubleDataValue> dataContainer = new(superMap, expectedDataTask);
            MetaCacheContainer metaContainer = new(Task.FromResult(mockMetadata.Object));
            
            metaContainer.AddDataContainer(dataContainer);
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            
            int metaKey = MetaCacheUtils.GetCacheKey(fileRef);
            memoryCache.Set(metaKey, metaContainer);

            int dataKey = MetaCacheUtils.GetCacheKey(superMap);
            memoryCache.Set(dataKey, dataContainer);
            
            DatabaseCache dbCache = new(memoryCache);

            // Act
            bool result = dbCache.TryGetDataSuperset(fileRef, map, out IMatrixMap? actualSupersetMap, 
                out Task<DoubleDataValue[]>? actualData, out DateTime? actualCached);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(actualSupersetMap, Is.SameAs(superMap));
                Assert.That(actualData, Is.SameAs(expectedDataTask));
                Assert.That(actualCached, Is.Not.Null);
            });
        }

        #endregion

        #region SetData

        [Test]
        public void SetData_WithoutMetaCacheHit_ThrowsInvalidOperationException()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("PxApiUnitTestsDb");
            PxFileRef fileRef = PxFileRef.Create("file1", database);
            MatrixMap map = new([
                new DimensionMap("dim1", ["value1"]),
                new DimensionMap("dim2", ["2025"])
            ]);
            DoubleDataValue[] data = [new DoubleDataValue(2, DataValueType.Exists)];
            Task<DoubleDataValue[]> dataTask = Task.FromResult(data);
            
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => 
                dbCache.SetData(fileRef, map, dataTask));
        }

        [Test]
        public async Task SetData_WithMetaCacheHit_UpdatesCacheWithExpectedValues()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("PxApiUnitTestsDb");
            PxFileRef fileRef = PxFileRef.Create("file1", database);
            MatrixMap map = new([
                new DimensionMap("dim1", ["value1"]),
                new DimensionMap("dim2", ["2025"])
            ]);
            DoubleDataValue[] data = [new DoubleDataValue(2, DataValueType.Exists)];
            Task<DoubleDataValue[]> dataTask = Task.FromResult(data);

            IReadOnlyMatrixMetadata metadata = await MatrixMetadataUtils.GetMetadataFromFixture(PxFixtures.MinimalPx.MINIMAL_UTF8_N);
            MetaCacheContainer metaContainer = new(Task.FromResult(metadata));
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);
            memoryCache.Set(HashCode.Combine(GetSeedForKeyword(META_SEED_VARNAME), fileRef), metaContainer);
            int mapCode = MetaCacheUtils.GetCacheKey(map);

            // Act
            dbCache.SetData(fileRef, map, dataTask);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(metaContainer.GetRelatedMaps(), Has.Count.EqualTo(1));
                Assert.That(metaContainer.GetRelatedMaps()[0], Is.SameAs(map));
                Assert.That(memoryCache.TryGetValue(mapCode, out DataCacheContainer<DoubleDataValue>? cachedContainer), Is.True);
                Assert.That(cachedContainer!.Data, Is.SameAs(dataTask));
            });
        }

        [Test]
        public async Task SetData_WithSubMaps_RemovesSubMapsFromCache()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("PxApiUnitTestsDb");
            PxFileRef fileRef = PxFileRef.Create("file1", database);
            MatrixMap map = new([
                new DimensionMap("dim1", ["value1"]),
                new DimensionMap("dim2", ["2024", "2025"])
            ]);
            MatrixMap subMap1 = new([
                new DimensionMap("dim1", ["value1"]),
                new DimensionMap("dim2", ["2024"])
            ]);
            DoubleDataValue[] data = [
                new DoubleDataValue(1, DataValueType.Exists),
                new DoubleDataValue(2, DataValueType.Exists)
            ];
            Task<DoubleDataValue[]> dataTask = Task.FromResult(data);

            IReadOnlyMatrixMetadata metadata = await MatrixMetadataUtils.GetMetadataFromFixture(PxFixtures.MinimalPx.MINIMAL_UTF8_N);
            MetaCacheContainer metaContainer = new(Task.FromResult(metadata));
            
            DataCacheContainer<DoubleDataValue> subContainer = new(
                subMap1,
                Task.FromResult<DoubleDataValue[]>([new DoubleDataValue(1, DataValueType.Exists)])
            );
            metaContainer.AddDataContainer(subContainer);

            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);

            // Pre-populate the cache with the meta container
            string metaSeed = GetSeedForKeyword(META_SEED_VARNAME);
            memoryCache.Set(HashCode.Combine(metaSeed, fileRef), metaContainer);

            // Pre-populate the cache with the submap data container
            int subMapKey = MetaCacheUtils.GetCacheKey(subMap1);
            int mapKey = MetaCacheUtils.GetCacheKey(map);
            memoryCache.Set(subMapKey, subContainer);

            // Act
            dbCache.SetData(fileRef, map, dataTask);

            // Assert
            Assert.Multiple(() =>
            {
                // The submap should have been removed from the cache
                Assert.That(memoryCache.TryGetValue(subMapKey, out _), Is.False);
                // The new map should be present
                Assert.That(memoryCache.TryGetValue(mapKey, out DataCacheContainer<DoubleDataValue>? cachedContainer), Is.True);
                Assert.That(cachedContainer!.Data, Is.SameAs(dataTask));
            });
        }

        #endregion

        #region ClearFileListCache

        [Test]
        public void ClearFileListCache_RemovesCachedFileList()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("PxApiUnitTestsDb");
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            string fileListSeed = GetSeedForKeyword(FILE_LIST_SEED_VARNAME);
            int cacheKey = HashCode.Combine(fileListSeed, database);
            
            Task<ImmutableSortedDictionary<string, PxFileRef>> fileList = Task.FromResult(
                ImmutableSortedDictionary<string, PxFileRef>.Empty
                    .Add("file1", PxFileRef.Create("file1", database)));
            
            memoryCache.Set(cacheKey, fileList);
            DatabaseCache dbCache = new(memoryCache);
            
            // Verify that cache has the file list
            Assert.That(memoryCache.TryGetValue(cacheKey, out _), Is.True);

            // Act
            dbCache.ClearFileListCache(database);

            // Assert
            Assert.That(memoryCache.TryGetValue(cacheKey, out _), Is.False);
        }

        #endregion

        #region ClearLastUpdatedCache

        [Test]
        public void ClearLastUpdatedCache_ClearsLastUpdatedForFile()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("PxApiUnitTestsDb");
            PxFileRef file = PxFileRef.Create("file1", database);
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            string lastUpdatedSeed = GetSeedForKeyword(LAST_UPDATED_SEED_VARNAME);
            
            DateTime now = DateTime.UtcNow;
            memoryCache.Set(HashCode.Combine(lastUpdatedSeed, file), Task.FromResult(now));
            
            DatabaseCache dbCache = new(memoryCache);
            
            // Verify that cache has the last updated timestamp
            Assert.That(memoryCache.TryGetValue(HashCode.Combine(lastUpdatedSeed, file), out _), Is.True);

            // Act
            dbCache.ClearLastUpdatedCache(file);

            // Assert
            Assert.That(memoryCache.TryGetValue(HashCode.Combine(lastUpdatedSeed, file), out _), Is.False);
        }

        #endregion

        private static string GetSeedForKeyword(string keyword)
        {
            // Use reflection to get the private field for the keyword hash seed
            string keywordSeed = typeof(DatabaseCache)
                .GetField(keyword, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .GetValue(null)!.ToString()!;
            return keywordSeed;
        }

        private static int GetMapHash(IMatrixMap map)
        {
            // Use reflection to get the private function for generating a matrix map hash code
            System.Reflection.MethodInfo? mapHashMethod = typeof(DatabaseCache)
                .GetMethod("MapHash", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return (int)mapHashMethod!.Invoke(null, [map])!;
        }
    }
}