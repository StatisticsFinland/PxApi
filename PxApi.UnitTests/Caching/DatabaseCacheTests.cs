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
using Px.Utils.Language;

namespace PxApi.UnitTests.Caching
{
    [TestFixture]
    internal class DatabaseCacheTests
    {
        private const string FILE_LIST_SEED_VARNAME = "FILE_LIST_SEED";
        private const string LAST_UPDATED_SEED_VARNAME = "LAST_UPDATED_SEED";
        private const string DATA_SEED_VARNAME = "DATA_SEED";
        private const string META_SEED_VARNAME = "META_SEED";
        private const string DATABASE_NAME_SEED_VARNAME = "DATABASE_NAME_SEED";

        private readonly PxFileRef _file1 = PxFileRef.ValidateAndCreate("file1", DataBaseRef.Create("PxApiUnitTestsDb"), ["statisticalProgram"]);
        private readonly PxFileRef _file2 = PxFileRef.ValidateAndCreate("file2", DataBaseRef.Create("PxApiUnitTestsDb"), ["statisticalProgram"]);

        [SetUp]
        public void SetUp()
        {
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                TestConfigFactory.MountedDb(0, "PxApiUnitTestsDb", "datasource/root/"),
                new Dictionary<string, string?>
                {
                    ["Cache:MaxSizeBytes"] = "524288000",
                    ["DataBases:0:CacheConfig:RevalidationIntervalMs"] = "1000",
                    ["DataBases:0:Custom:ModifiedCheckIntervalMs"] = "1000",
                    ["DataBases:0:Custom:FileListingCacheDurationMs"] = "10000"
                }
            );
            TestConfigFactory.BuildAndLoad(configData);
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
                    .Add("file1", _file1)
                    .Add("file2", _file2));
            string fileListSeed = GetSeedForKeyword(FILE_LIST_SEED_VARNAME);
            memoryCache.Set(HashCode.Combine(fileListSeed, database), expectedFiles);
            DatabaseCache dbCache = new(memoryCache);

            // Act
            bool result = dbCache.TryGetFileList(database, out Task<ImmutableSortedDictionary<string, PxFileRef>>? actualFiles);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.True);
                Assert.That(actualFiles, Is.SameAs(expectedFiles));
            }
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
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.False);
                Assert.That(actualFiles, Is.Null);
            }
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
                    .Add("file1", _file1)
                    .Add("file2", _file2));
            
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);
            string fileListSeed = GetSeedForKeyword(FILE_LIST_SEED_VARNAME);

            // Act
            dbCache.SetFileList(database, files);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(memoryCache.TryGetValue(
                    HashCode.Combine(fileListSeed, database),
                    out Task<ImmutableSortedDictionary<string, PxFileRef>>? cachedFiles),
                    Is.True);
                Assert.That(cachedFiles, Is.SameAs(files));
            }
        }

        #endregion

        #region TryGetLastUpdated

        [Test]
        public void TryGetLastUpdated_WithCacheHit_ReturnsTruewithLastUpdated()
        {
            // Arrange
            Task<DateTime> expectedLastUpdated = Task.FromResult(DateTime.UtcNow);
            
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            string lastUpdatedSeed = GetSeedForKeyword(LAST_UPDATED_SEED_VARNAME);
            memoryCache.Set(HashCode.Combine(lastUpdatedSeed, _file1), expectedLastUpdated);
            DatabaseCache dbCache = new(memoryCache);

            // Act
            bool result = dbCache.TryGetLastUpdated(_file1, out Task<DateTime>? actualLastUpdated);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.True);
                Assert.That(actualLastUpdated, Is.SameAs(expectedLastUpdated));
            }
        }

        [Test]
        public void TryGetLastUpdated_WithCacheMiss_ReturnsFalse()
        {
            // Arrange
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);

            // Act
            bool result = dbCache.TryGetLastUpdated(_file1, out Task<DateTime>? actualLastUpdated);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.False);
                Assert.That(actualLastUpdated, Is.Null);
            }
        }

        #endregion

        #region SetLastUpdated

        [Test]
        public void SetLastUpdated_WithFileRefAndTimeStamp_SetsCacheWithExpectedValues()
        {
            // Arrange
            Task<DateTime> lastUpdated = Task.FromResult(DateTime.UtcNow);
            string lastUpdatedSeed = GetSeedForKeyword(LAST_UPDATED_SEED_VARNAME);
            
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);
                
            // Act
            dbCache.SetLastUpdated(_file1, lastUpdated);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(memoryCache.TryGetValue(HashCode.Combine(lastUpdatedSeed, _file1), out Task<DateTime>? cachedLastUpdated), Is.True);
                Assert.That(cachedLastUpdated, Is.SameAs(lastUpdated));
            }
        }

        #endregion

        #region TryGetMetadata

        [Test]
        public void TryGetMetadata_WithCacheHit_ReturnsTrueWithMetadataContainer()
        {
            // Arrange
            Mock<IReadOnlyMatrixMetadata> mockMetadata = new();
            MetaCacheContainer expectedMetaContainer = new(Task.FromResult(mockMetadata.Object));
            
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            string metaSeed = GetSeedForKeyword(META_SEED_VARNAME);
            int metaKey = HashCode.Combine(metaSeed, _file1);
            memoryCache.Set(metaKey, expectedMetaContainer);
            
            DatabaseCache dbCache = new(memoryCache);

            // Act
            bool result = dbCache.TryGetMetadata(_file1, out MetaCacheContainer? actualMetaContainer);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.True);
                Assert.That(actualMetaContainer, Is.SameAs(expectedMetaContainer));
            }
        }

        [Test]
        public void TryGetMetadata_WithCacheMiss_ReturnsFalse()
        {
            // Arrange
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);

            // Act
            bool result = dbCache.TryGetMetadata(_file1, out MetaCacheContainer? actualMetaContainer);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.False);
                Assert.That(actualMetaContainer, Is.Null);
            }
        }

        #endregion

        #region SetMetadata

        [Test]
        public void SetMetadata_WithPxFileRefAndMetadataContainer_SetsCacheWithExpectedValues()
        {
            // Arrange
            Mock<IReadOnlyMatrixMetadata> mockMetadata = new();
            MetaCacheContainer metaContainer = new(Task.FromResult(mockMetadata.Object));
            
            MemoryCache cache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(cache);
            string metaSeed = GetSeedForKeyword(META_SEED_VARNAME);
            int cacheKey = HashCode.Combine(metaSeed, _file1);
            
            // Act
            dbCache.SetMetadata(_file1, metaContainer);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(cache.TryGetValue(cacheKey, out MetaCacheContainer? cachedMetaContainer), Is.True);
                Assert.That(cachedMetaContainer, Is.SameAs(metaContainer));
            }
        }

        #endregion

        #region TryRemoveMeta

        [Test]
        public void TryRemoveMeta_WithCacheHit_RemovesMetaFromCache()
        {
            // Arrange
            Mock<IReadOnlyMatrixMetadata> mockMetadata = new();
            MetaCacheContainer metaContainer = new(Task.FromResult(mockMetadata.Object));
            
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            string metaSeed = GetSeedForKeyword(META_SEED_VARNAME);
            int cacheKey = HashCode.Combine(metaSeed, _file1);
            memoryCache.Set(cacheKey, metaContainer);
            
            DatabaseCache dbCache = new(memoryCache);

            // Act
            dbCache.TryRemoveMeta(_file1);

            // Assert
            Assert.That(memoryCache.TryGetValue(cacheKey, out _), Is.False);
        }

        [Test]
        public void TryRemoveMeta_WithCacheMiss_DoesNotThrow()
        {
            // Arrange
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);

            // Act & Assert
            using (Assert.EnterMultipleScope())
            {  
                Assert.That(memoryCache.Keys.Count(), Is.Zero);
                Assert.That(dbCache.TryGetMetadata(_file1, out _), Is.False);
                Assert.DoesNotThrow(() => dbCache.TryRemoveMeta(_file1));
            }
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
            memoryCache.Set((dataSeed, _file1, mapHash), dataContainer);
            
            DatabaseCache dbCache = new(memoryCache);

            // Act
            bool result = dbCache.TryGetData(_file1, map, out Task<DoubleDataValue[]>? actualData, out DateTime? actualCached);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.True);
                Assert.That(actualData, Is.SameAs(expectedDataTask));
                Assert.That(actualCached, Is.Not.Null);
            }
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
            bool result = dbCache.TryGetData(_file1, map, out Task<DoubleDataValue[]>? actualData, out DateTime? actualCached);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.False);
                Assert.That(actualData, Is.Null);
                Assert.That(actualCached, Is.Null);
            }
        }

        #endregion

        #region TryGetDataSuperset

        [TestCase("differentTable", "PxApiUnitTestsDb", "statisticalProgram")]
        [TestCase("file1", "OtherDb", "statisticalProgram")]
        [TestCase("file1", "PxApiUnitTestsDb", "differentHierarchy")]
        public void TryGetData_WithDifferentFileReference_DoesNotReturnCachedData(string tableId, string databaseId, string hierarchy)
        {
            PxFileRef otherFile = PxFileRef.ValidateAndCreate(tableId, DataBaseRef.Create(databaseId), [hierarchy]);
            MatrixMap map = new([new DimensionMap("dimension", ["value"])]);
            MetaCacheContainer metadata = new(Task.FromResult(Mock.Of<IReadOnlyMatrixMetadata>()));
            using MemoryCache memory = new(new MemoryCacheOptions());
            DatabaseCache cache = new(memory);
            cache.SetMetadata(_file1, metadata);
            cache.SetData(_file1, metadata, map, Task.FromResult<int[]>([1]));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(cache.TryGetData<int>(_file1, map, out _, out _), Is.True);
                Assert.That(cache.TryGetData<int>(otherFile, map, out Task<int[]>? data, out DateTime? cached), Is.False);
                Assert.That(data, Is.Null);
                Assert.That(cached, Is.Null);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SetData_WithIdenticalMapsForDifferentTables_KeepsExactAndSupersetDataSeparate(bool differentDatabase)
        {
            TestConfigFactory.BuildAndLoad(TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                TestConfigFactory.MountedDb(0, _file1.DataBase.Id),
                TestConfigFactory.MountedDb(1, "OtherDb")));
            PxFileRef otherFile = differentDatabase
                ? PxFileRef.ValidateAndCreate(_file1.Id, DataBaseRef.Create("OtherDb"), ["statisticalProgram"])
                : _file2;
            MatrixMap map = new([new DimensionMap("dimension", ["one", "two"])]);
            MatrixMap subset = new([new DimensionMap("dimension", ["two"])]);
            MetaCacheContainer firstMetadata = new(Task.FromResult(Mock.Of<IReadOnlyMatrixMetadata>()));
            MetaCacheContainer otherMetadata = new(Task.FromResult(Mock.Of<IReadOnlyMatrixMetadata>()));
            Task<int[]> firstData = Task.FromResult<int[]>([1, 2]);
            Task<int[]> otherData = Task.FromResult<int[]>([3, 4]);
            using MemoryCache memory = new(new MemoryCacheOptions());
            DatabaseCache cache = new(memory);
            cache.SetMetadata(_file1, firstMetadata);
            cache.SetMetadata(otherFile, otherMetadata);
            cache.SetData(_file1, firstMetadata, map, firstData);
            cache.SetData(otherFile, otherMetadata, map, otherData);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(cache.TryGetData(_file1, map, out Task<int[]>? firstExact, out _), Is.True);
                Assert.That(firstExact, Is.SameAs(firstData));
                Assert.That(cache.TryGetData(otherFile, map, out Task<int[]>? otherExact, out _), Is.True);
                Assert.That(otherExact, Is.SameAs(otherData));
                Assert.That(cache.TryGetDataSuperset(_file1, subset, out _, out Task<int[]>? firstSuper, out _), Is.True);
                Assert.That(firstSuper, Is.SameAs(firstData));
                Assert.That(cache.TryGetDataSuperset(otherFile, subset, out _, out Task<int[]>? otherSuper, out _), Is.True);
                Assert.That(otherSuper, Is.SameAs(otherData));
            }
        }

        [Test]
        public void SetData_WithLargerMap_RemovesOnlyOwningTablesSubmap()
        {
            MatrixMap subset = new([new DimensionMap("dimension", ["one"])]);
            MatrixMap superset = new([new DimensionMap("dimension", ["one", "two"])]);
            MetaCacheContainer firstMetadata = new(Task.FromResult(Mock.Of<IReadOnlyMatrixMetadata>()));
            MetaCacheContainer otherMetadata = new(Task.FromResult(Mock.Of<IReadOnlyMatrixMetadata>()));
            Task<int[]> otherData = Task.FromResult<int[]>([3]);
            using MemoryCache memory = new(new MemoryCacheOptions());
            DatabaseCache cache = new(memory);
            cache.SetMetadata(_file1, firstMetadata);
            cache.SetMetadata(_file2, otherMetadata);
            cache.SetData(_file1, firstMetadata, subset, Task.FromResult<int[]>([1]));
            cache.SetData(_file2, otherMetadata, subset, otherData);

            cache.SetData(_file1, firstMetadata, superset, Task.FromResult<int[]>([1, 2]));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(cache.TryGetData<int>(_file1, subset, out _, out _), Is.False);
                Assert.That(cache.TryGetData<int>(_file1, superset, out _, out _), Is.True);
                Assert.That(cache.TryGetData(_file2, subset, out Task<int[]>? retained, out _), Is.True);
                Assert.That(retained, Is.SameAs(otherData));
            }
        }

        [Test]
        public void SetData_WhenSourceTaskFails_PreservesOtherTablesData()
        {
            MatrixMap map = new([new DimensionMap("dimension", ["one"])]);
            MetaCacheContainer firstMetadata = new(Task.FromResult(Mock.Of<IReadOnlyMatrixMetadata>()));
            MetaCacheContainer otherMetadata = new(Task.FromResult(Mock.Of<IReadOnlyMatrixMetadata>()));
            TaskCompletionSource<int[]> failedData = new();
            Task<int[]> otherData = Task.FromResult<int[]>([3]);
            using MemoryCache memory = new(new MemoryCacheOptions());
            DatabaseCache cache = new(memory);
            cache.SetMetadata(_file1, firstMetadata);
            cache.SetMetadata(_file2, otherMetadata);
            cache.SetData(_file1, firstMetadata, map, failedData.Task);
            cache.SetData(_file2, otherMetadata, map, otherData);

            failedData.SetException(new InvalidOperationException("Source read failed."));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(cache.TryGetData<int>(_file1, map, out _, out _), Is.False);
                Assert.That(cache.TryGetData(_file2, map, out Task<int[]>? retained, out _), Is.True);
                Assert.That(retained, Is.SameAs(otherData));
            }
        }

        [TestCase(EvictionReason.Removed)]
        [TestCase(EvictionReason.Replaced)]
        public void OnMetaCacheEvicted_RemovesOnlyOwningTablesData(EvictionReason reason)
        {
            MatrixMap map = new([new DimensionMap("dimension", ["one"])]);
            MetaCacheContainer firstMetadata = new(Task.FromResult(Mock.Of<IReadOnlyMatrixMetadata>()));
            MetaCacheContainer otherMetadata = new(Task.FromResult(Mock.Of<IReadOnlyMatrixMetadata>()));
            Task<int[]> otherData = Task.FromResult<int[]>([3]);
            using MemoryCache memory = new(new MemoryCacheOptions());
            ICacheEntry? firstMetadataEntry = null;
            Mock<IMemoryCache> forwardingCache = new();
            forwardingCache.Setup(item => item.CreateEntry(It.IsAny<object>()))
                .Returns<object>(key =>
                {
                    ICacheEntry entry = memory.CreateEntry(key);
                    if (key.Equals(MetaCacheUtils.GetCacheKey(_file1))) firstMetadataEntry = entry;
                    return entry;
                });
            forwardingCache.Setup(item => item.TryGetValue(It.IsAny<object>(), out It.Ref<object?>.IsAny))
                .Returns((object key, out object? value) => memory.TryGetValue(key, out value));
            forwardingCache.Setup(item => item.Remove(It.IsAny<object>()))
                .Callback<object>(memory.Remove);
            DatabaseCache cache = new(forwardingCache.Object);
            cache.SetMetadata(_file1, firstMetadata);
            cache.SetMetadata(_file2, otherMetadata);
            cache.SetData(_file1, firstMetadata, map, Task.FromResult<int[]>([1]));
            cache.SetData(_file2, otherMetadata, map, otherData);
            PostEvictionCallbackRegistration callback = firstMetadataEntry!.PostEvictionCallbacks.Single();

            callback.EvictionCallback!(firstMetadataEntry.Key, firstMetadata, reason, callback.State);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(callback.State, Is.EqualTo(_file1));
                Assert.That(cache.TryGetData<int>(_file1, map, out _, out _), Is.False);
                Assert.That(cache.TryGetData(_file2, map, out Task<int[]>? retained, out _), Is.True);
                Assert.That(retained, Is.SameAs(otherData));
            }
        }

        [Test]
        public void TryGetDataSuperSet_WithCacheMiss_ReturnsFalse()
        {
            // Arrange
            MatrixMap map = new([
                new DimensionMap("dim1", ["value1"]),
                new DimensionMap("dim2", ["2025"])
            ]);
            
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);

            // Act
            bool result = dbCache.TryGetDataSuperset(_file1, map, out IMatrixMap? actualSupersetMap, 
                out Task<DoubleDataValue[]>? actualData, out DateTime? actualCached);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.False);
                Assert.That(actualSupersetMap, Is.Null);
                Assert.That(actualData, Is.Null);
                Assert.That(actualCached, Is.Null);
            }
        }

        [Test]
        public void TryGetDataSuperSet_WithCacheHitWithoutSuperMap_ReturnsFalse()
        {
            // Arrange
            MatrixMap map = new([
                new DimensionMap("dim1", ["value1"]),
                new DimensionMap("dim2", ["2025"])
            ]);
            
            Mock<IReadOnlyMatrixMetadata> mockMetadata = new();
            MetaCacheContainer metaContainer = new(Task.FromResult(mockMetadata.Object));
            
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            string metaSeed = GetSeedForKeyword(META_SEED_VARNAME);
            memoryCache.Set(HashCode.Combine(metaSeed, _file1), metaContainer);
            DatabaseCache dbCache = new(memoryCache);

            // Act
            bool result = dbCache.TryGetDataSuperset(_file1, map, out IMatrixMap? actualSupersetMap, 
                out Task<DoubleDataValue[]>? actualData, out DateTime? actualCached);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.False);
                Assert.That(actualSupersetMap, Is.Null);
                Assert.That(actualData, Is.Null);
                Assert.That(actualCached, Is.Null);
            }
        }

        [Test]
        public void TryGetDataSuperSet_WithCacheHitWithSuperMapWithContainerMiss_ReturnsFalse()
        {
            // Arrange
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
            memoryCache.Set(HashCode.Combine(metaSeed, _file1), metaContainer);
            DatabaseCache dbCache = new(memoryCache);

            // Act
            bool result = dbCache.TryGetDataSuperset(_file1, map, out IMatrixMap? actualSupersetMap, 
                out Task<DoubleDataValue[]>? actualData, out DateTime? actualCached);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.False);
                Assert.That(actualSupersetMap, Is.Null);
                Assert.That(actualData, Is.Null);
                Assert.That(actualCached, Is.Null);
            }
        }

        [Test]
        public void TryGetDataSuperSet_WithCachedSuperSetMap_ReturnsTrueWithSupersetDataContainer()
        {
            // Arrange
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
            
            int metaKey = MetaCacheUtils.GetCacheKey(_file1);
            memoryCache.Set(metaKey, metaContainer);

            (string, PxFileRef, int) dataKey = MetaCacheUtils.GetCacheKey(_file1, superMap);
            memoryCache.Set(dataKey, dataContainer);
            
            DatabaseCache dbCache = new(memoryCache);

            // Act
            bool result = dbCache.TryGetDataSuperset(_file1, map, out IMatrixMap? actualSupersetMap, 
                out Task<DoubleDataValue[]>? actualData, out DateTime? actualCached);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.True);
                Assert.That(actualSupersetMap, Is.SameAs(superMap));
                Assert.That(actualData, Is.SameAs(expectedDataTask));
                Assert.That(actualCached, Is.Not.Null);
            }
        }

        #endregion

        #region SetData

        [Test]
        public void SetData_WithoutMetaCacheHit_SkipsCachingAndNotifiesObserver()
        {
            // Arrange
            MatrixMap map = new([
                new DimensionMap("dim1", ["value1"]),
                new DimensionMap("dim2", ["2025"])
            ]);
            DoubleDataValue[] data = [new DoubleDataValue(2, DataValueType.Exists)];
            Task<DoubleDataValue[]> dataTask = Task.FromResult(data);
            
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            Mock<ICacheObserver> observer = new();
            DatabaseCache dbCache = new(memoryCache, observer.Object);
            MetaCacheContainer metaContainer = new(Task.FromResult(Mock.Of<IReadOnlyMatrixMetadata>()));

            dbCache.SetData(_file1, metaContainer, map, dataTask);

            Assert.That(dbCache.TryGetData<DoubleDataValue>(_file1, map, out _, out _), Is.False);
            observer.Verify(item => item.RecordDataCacheWriteSkipped(), Times.Once);
            observer.VerifyNoOtherCalls();
        }

        [Test]
        public void SetData_WithoutMetaCacheHitOrObserver_SkipsCaching()
        {
            MatrixMap map = new([new DimensionMap("dim1", ["value1"])]);
            Task<DoubleDataValue[]> dataTask = Task.FromResult<DoubleDataValue[]>([]);
            using MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);
            MetaCacheContainer metaContainer = new(Task.FromResult(Mock.Of<IReadOnlyMatrixMetadata>()));

            dbCache.SetData(_file1, metaContainer, map, dataTask);

            Assert.That(dbCache.TryGetData<DoubleDataValue>(_file1, map, out _, out _), Is.False);
        }

        [Test]
        public async Task SetData_WithMetaCacheHit_UpdatesCacheWithExpectedValues()
        {
            // Arrange
            MatrixMap map = new([
                new DimensionMap("dim1", ["value1"]),
                new DimensionMap("dim2", ["2025"])
            ]);
            DoubleDataValue[] data = [new DoubleDataValue(2, DataValueType.Exists)];
            Task<DoubleDataValue[]> dataTask = Task.FromResult(data);

            IReadOnlyMatrixMetadata metadata = await MatrixMetadataUtils.GetMetadataFromFixture(PxFixtures.MinimalPx.MINIMAL_UTF8_N);
            MetaCacheContainer metaContainer = new(Task.FromResult(metadata));
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            Mock<ICacheObserver> observer = new();
            DatabaseCache dbCache = new(memoryCache, observer.Object);
            memoryCache.Set(HashCode.Combine(GetSeedForKeyword(META_SEED_VARNAME), _file1), metaContainer);
            (string, PxFileRef, int) mapCode = MetaCacheUtils.GetCacheKey(_file1, map);

            // Act
            dbCache.SetData(_file1, metaContainer, map, dataTask);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(metaContainer.GetRelatedMaps(), Has.Count.EqualTo(1));
                Assert.That(metaContainer.GetRelatedMaps()[0], Is.SameAs(map));
                Assert.That(memoryCache.TryGetValue(mapCode, out DataCacheContainer<DoubleDataValue>? cachedContainer), Is.True);
                Assert.That(cachedContainer!.Data, Is.SameAs(dataTask));
            }
            observer.VerifyNoOtherCalls();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void SetData_WithReplacedMetaContainer_PreservesCurrentCache(bool withObserver)
        {
            MatrixMap map = new([new DimensionMap("dim1", ["value1", "value2"])]);
            MatrixMap subMap = new([new DimensionMap("dim1", ["value1"])]);
            Task<IReadOnlyMatrixMetadata> metadata = Task.FromResult(Mock.Of<IReadOnlyMatrixMetadata>());
            MetaCacheContainer expectedMetaContainer = new(metadata);
            MetaCacheContainer currentMetaContainer = new(metadata);
            Task<DoubleDataValue[]> currentData = Task.FromResult<DoubleDataValue[]>([
                new(1, DataValueType.Exists),
                new(2, DataValueType.Exists)
            ]);
            Task<DoubleDataValue[]> subData = Task.FromResult<DoubleDataValue[]>([new(1, DataValueType.Exists)]);
            Task<DoubleDataValue[]> staleData = Task.FromResult<DoubleDataValue[]>([
                new(3, DataValueType.Exists),
                new(4, DataValueType.Exists)
            ]);
            DataCacheContainer<DoubleDataValue> currentDataContainer = new(map, currentData);
            DataCacheContainer<DoubleDataValue> subDataContainer = new(subMap, subData);
            currentMetaContainer.AddDataContainer(currentDataContainer);
            currentMetaContainer.AddDataContainer(subDataContainer);
            using MemoryCache memoryCache = new(new MemoryCacheOptions());
            memoryCache.Set(MetaCacheUtils.GetCacheKey(_file1), currentMetaContainer);
            memoryCache.Set(MetaCacheUtils.GetCacheKey(_file1, map), currentDataContainer);
            memoryCache.Set(MetaCacheUtils.GetCacheKey(_file1, subMap), subDataContainer);
            Mock<ICacheObserver> observer = new();
            DatabaseCache dbCache = new(memoryCache, withObserver ? observer.Object : null);

            dbCache.SetData(_file1, expectedMetaContainer, map, staleData);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(dbCache.TryGetMetadata(_file1, out MetaCacheContainer? cachedMetadata), Is.True);
                Assert.That(cachedMetadata, Is.SameAs(currentMetaContainer));
                Assert.That(dbCache.TryGetData(_file1, map, out Task<DoubleDataValue[]>? cachedData, out _), Is.True);
                Assert.That(cachedData, Is.SameAs(currentData));
                Assert.That(dbCache.TryGetData(_file1, subMap, out Task<DoubleDataValue[]>? cachedSubData, out _), Is.True);
                Assert.That(cachedSubData, Is.SameAs(subData));
                Assert.That(currentMetaContainer.GetRelatedMaps(), Is.EqualTo(new[] { map, subMap }));
                Assert.That(expectedMetaContainer.GetRelatedMaps(), Is.Empty);
            }
            observer.Verify(item => item.RecordDataCacheWriteSkipped(), withObserver ? Times.Once() : Times.Never());
            observer.VerifyNoOtherCalls();
        }

        [Test]
        public async Task SetData_WithSubMaps_RemovesSubMapsFromCache()
        {
            // Arrange
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
            memoryCache.Set(HashCode.Combine(metaSeed, _file1), metaContainer);

            // Pre-populate the cache with the submap data container
            (string, PxFileRef, int) subMapKey = MetaCacheUtils.GetCacheKey(_file1, subMap1);
            (string, PxFileRef, int) mapKey = MetaCacheUtils.GetCacheKey(_file1, map);
            memoryCache.Set(subMapKey, subContainer);

            // Act
            dbCache.SetData(_file1, metaContainer, map, dataTask);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                // The submap should have been removed from the cache
                Assert.That(memoryCache.TryGetValue(subMapKey, out _), Is.False);
                // The new map should be present
                Assert.That(memoryCache.TryGetValue(mapKey, out DataCacheContainer<DoubleDataValue>? cachedContainer), Is.True);
                Assert.That(cachedContainer!.Data, Is.SameAs(dataTask));
            }
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
                    .Add("file1", _file1));
            
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
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            string lastUpdatedSeed = GetSeedForKeyword(LAST_UPDATED_SEED_VARNAME);
            
            DateTime now = DateTime.UtcNow;
            memoryCache.Set(HashCode.Combine(lastUpdatedSeed, _file1), Task.FromResult(now));
            
            DatabaseCache dbCache = new(memoryCache);
            
            // Verify that cache has the last updated timestamp
            Assert.That(memoryCache.TryGetValue(HashCode.Combine(lastUpdatedSeed, _file1), out _), Is.True);

            // Act
            dbCache.ClearLastUpdatedCache(_file1);

            // Assert
            Assert.That(memoryCache.TryGetValue(HashCode.Combine(lastUpdatedSeed, _file1), out _), Is.False);
        }

        #endregion

        #region OnMetaCacheEvicted Tests

        #endregion

        #region TryGetDatabaseName

        [Test]
        public void TryGetDatabaseName_WithCacheHit_ReturnsTrueWithNameTask()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("PxApiUnitTestsDb");
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);
            MultilanguageString expectedName = new(new Dictionary<string, string>
            {
                {"fi", "Suomi"},
                {"sv", "Sverige"},
                {"en", "Finland"}
            });
            Task<MultilanguageString> nameTask = Task.FromResult(expectedName);
            string seed = GetSeedForKeyword(DATABASE_NAME_SEED_VARNAME);
            memoryCache.Set(HashCode.Combine(seed, database), nameTask);

            // Act
            bool result = dbCache.TryGetDatabaseName(database, out Task<MultilanguageString>? actualTask);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.True);
                Assert.That(actualTask, Is.SameAs(nameTask));
            }
        }

        [Test]
        public void TryGetDatabaseName_WithCacheMiss_ReturnsFalse()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("PxApiUnitTestsDb");
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);

            // Act
            bool result = dbCache.TryGetDatabaseName(database, out Task<MultilanguageString>? actualTask);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.False);
                Assert.That(actualTask, Is.Null);
            }
        }

        #endregion

        #region SetDatabaseName

        [Test]
        public void SetDatabaseName_WithValidName_SetsCache()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("PxApiUnitTestsDb");
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);
            MultilanguageString nameValue = new(new Dictionary<string, string>
            {
                {"fi", "Suomi"},
                {"sv", "Sverige"},
                {"en", "Finland"}
            });
            Task<MultilanguageString> nameTask = Task.FromResult(nameValue);
            string seed = GetSeedForKeyword(DATABASE_NAME_SEED_VARNAME);
            int key = HashCode.Combine(seed, database);

            // Act
            dbCache.SetDatabaseName(database, nameTask);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(memoryCache.TryGetValue(key, out Task<MultilanguageString>? cachedTask), Is.True);
                Assert.That(cachedTask, Is.SameAs(nameTask));
            }
        }

        #endregion

        #region ClearDatabaseNameCache

        [Test]
        public void ClearDatabaseNameCache_RemovesCachedName()
        {
            // Arrange
            DataBaseRef database = DataBaseRef.Create("PxApiUnitTestsDb");
            MemoryCache memoryCache = new(new MemoryCacheOptions());
            DatabaseCache dbCache = new(memoryCache);
            MultilanguageString nameValue = new(new Dictionary<string, string>
            {
                {"fi", "Suomi"}
            });
            Task<MultilanguageString> nameTask = Task.FromResult(nameValue);
            dbCache.SetDatabaseName(database, nameTask);
            string seed = GetSeedForKeyword(DATABASE_NAME_SEED_VARNAME);
            int key = HashCode.Combine(seed, database);
            Assert.That(memoryCache.TryGetValue(key, out _), Is.True);

            // Act
            dbCache.ClearDatabaseNameCache(database);

            // Assert
            Assert.That(memoryCache.TryGetValue(key, out _), Is.False);
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