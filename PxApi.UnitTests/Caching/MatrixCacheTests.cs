using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Moq;
using Px.Utils.Models.Metadata;
using PxApi.Caching;
using PxApi.Configuration;
using PxApi.Models;
using PxApi.UnitTests.Utils;

namespace PxApi.UnitTests.Caching
{
    [TestFixture]
    internal class MatrixCacheTests
    {
        private readonly PxFileRef _tableId = PxFileRef.Create("testTable", DataBaseRef.Create("PxApiUnitTestsDb"));
        private static Task<IReadOnlyMatrixMetadata> MatrixMetadata
        {
            get
            {
                Mock<IReadOnlyMatrixMetadata> mock = new();
                return Task.FromResult(mock.Object);
            }
        }

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

        [Test]
        public void TryGetMetadata_WhenCacheContainsMetadata_ReturnsTrue()
        {
            // Arrange
            // OBS: This needs to of type object, so that the extension method of IMemoryCache is not mocked.
            object? mockMetaContainer = new MetaCacheContainer(MatrixMetadata);

            Mock<IMemoryCache> memoryCacheMock = new();
            memoryCacheMock.Setup(m => m.TryGetValue(It.Is<int>(k => k == MetaCacheUtils.GetCacheKey(_tableId)), out mockMetaContainer))
                .Returns(true);

            DatabaseCache sut = new(memoryCacheMock.Object);

            // Act
            bool result = sut.TryGetMetadata(_tableId, out MetaCacheContainer? metaContainer);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(metaContainer, Is.Not.Null);
            });
        }

        [Test]
        public void TryGetMetadata_WhenCacheDoesNotContainMetadata_ReturnsFalse()
        {
            // Arrange
            object? cachedMetadata = null;
            
            Mock<IMemoryCache> memoryCacheMock = new();
            memoryCacheMock.Setup(m => m.TryGetValue(It.Is<int>(k => k == MetaCacheUtils.GetCacheKey(_tableId)), out cachedMetadata))
                .Returns(false);
            DatabaseCache sut = new(memoryCacheMock.Object);

            // Act
            bool result = sut.TryGetMetadata(_tableId, out MetaCacheContainer? metaContainer);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(metaContainer, Is.Null);
            });
        }

        [Test]
        public void SetMetadata_CallsMemoryCacheCreateEntry()
        {
            // Arrange
            Mock<IMemoryCache> memoryCacheMock = new();
            Mock<ICacheEntry> cacheEntryMock = new();
            cacheEntryMock.Setup(c => c.PostEvictionCallbacks)
                .Returns([]);
            memoryCacheMock.Setup(m => m.CreateEntry(It.Is<int>(k => k == MetaCacheUtils.GetCacheKey(_tableId))))
                .Returns(cacheEntryMock.Object);

            DatabaseCache sut = new(memoryCacheMock.Object);

            // Act
            MetaCacheContainer metaContainer = new(MatrixMetadata);
            sut.SetMetadata(_tableId, metaContainer);

            // Assert
            memoryCacheMock.Verify(
                m => m.CreateEntry(
                    It.Is<int>(k => k == MetaCacheUtils.GetCacheKey(_tableId))
                ),
                Times.Once);
        }

        [Test]
        public void SetMetadata_RegistersEvictionCallback()
        {
            // Arrange
            MemoryCacheEntryOptions options = new();

            Mock<IMemoryCache> memoryCacheMock = new();
            Mock<ICacheEntry> cacheEntryMock = new();
            cacheEntryMock.Setup(c => c.PostEvictionCallbacks)
                .Returns([]);
            ICacheEntry cacheEntry = cacheEntryMock.Object;
            memoryCacheMock.Setup(m => m.CreateEntry(It.Is<int>(k => k == MetaCacheUtils.GetCacheKey(_tableId))))
                .Returns(cacheEntry);

            DatabaseCache sut = new(memoryCacheMock.Object);

            // Act
            MetaCacheContainer metaContainer = new(MatrixMetadata);
            sut.SetMetadata(_tableId, metaContainer);

            // Assert
            Assert.That(cacheEntry.PostEvictionCallbacks, Has.Count.EqualTo(1));
        }

        [Test]
        public void TryGetData_WhenCacheContainsData_ReturnsTrue()
        {
            // Arrange
            Task<int[]> testData = Task.FromResult<int[]>( [ 1, 2, 3, 4 ]);
            MatrixMap matrixMap = new([
                new DimensionMap("dim1", ["val1", "val2"]),
                new DimensionMap("dim2", ["val1", "val2"]),
                ]);
            object? cachedData = new DataCacheContainer<int>(matrixMap, testData);

            Mock<IMemoryCache> memoryCacheMock = new();
            memoryCacheMock.Setup(m => m.TryGetValue(It.Is<int>(k => k == MetaCacheUtils.GetCacheKey(matrixMap)), out cachedData))
                .Returns(true);
            DatabaseCache sut = new(memoryCacheMock.Object);

            // Act
            bool result = sut.TryGetData(matrixMap, out Task<int[]>? data, out DateTime? cached);

            // Assert
            Assert.Multiple(async () =>
            {
                Assert.That(result, Is.True);
                Assert.That(await data!, Is.EqualTo(await testData));
                Assert.That(cached!, Is.LessThanOrEqualTo(DateTime.UtcNow));
            });
        }

        [Test]
        public void TryGetData_WhenCacheDoesNotContainData_ReturnsFalse()
        {
            // Arrange
            object? cachedData = null;
            MatrixMap matrixMap = new([
                new DimensionMap("dim1", ["val1", "val2"]),
                new DimensionMap("dim2", ["val1", "val2"]),
                ]);
            
            Mock<IMemoryCache> memoryCacheMock = new();
            memoryCacheMock.Setup(m => m.TryGetValue(It.IsAny<object>(), out cachedData))
                .Returns(false);
            DatabaseCache sut = new(memoryCacheMock.Object);

            // Act
            bool result = sut.TryGetData(matrixMap, out Task<int[]>? data, out DateTime? cached);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(data, Is.Null);
                Assert.That(cached, Is.Null);
            });
        }

        [Test]
        public void TryGetDataSuperset_WhenMetaContainerFoundButNoSupersetMap_ReturnsFalse()
        {
            // Arrange
            object? metaContainer = new MetaCacheContainer(MatrixMetadata);
            MatrixMap matrixMap = new([
                new DimensionMap("dim1", ["val1", "val2"]),
                new DimensionMap("dim2", ["val1", "val2"]),
                ]);
            
            // Setup meta container cache retrieval
            Mock<IMemoryCache> memoryCacheMock = new();
            memoryCacheMock.Setup(m => m.TryGetValue(It.IsAny<int>(), out metaContainer))
                .Returns(true);
            DatabaseCache sut = new(memoryCacheMock.Object);
            
            // Act
            bool result = sut.TryGetDataSuperset(
                _tableId,
                matrixMap,
                out IMatrixMap? supersetMap,
                out Task<int[]>? data,
                out DateTime? cached
            );

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(supersetMap, Is.Null);
                Assert.That(data, Is.Null);
                Assert.That(cached, Is.Null);
            });
        }

        [Test]
        public void TryGetDataSuperset_WhenMetaContainerNotFound_ReturnsFalse()
        {
            // Arrange
            object? metaContainer = new MetaCacheContainer(MatrixMetadata);
            MatrixMap matrixMap = new([
                new DimensionMap("dim1", ["val1", "val2"]),
                new DimensionMap("dim2", ["val1", "val2"]),
                ]);
            
            // Setup meta container cache retrieval
            Mock<IMemoryCache> memoryCacheMock = new();
            memoryCacheMock.Setup(m => m.TryGetValue(It.IsAny<int>(), out metaContainer))
                .Returns(false);
            DatabaseCache sut = new(memoryCacheMock.Object);
            
            // Act
            bool result = sut.TryGetDataSuperset(_tableId, matrixMap, out IMatrixMap? supersetMap, out Task<int[]>? data, out DateTime? cached);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(supersetMap, Is.Null);
                Assert.That(data, Is.Null);
                Assert.That(cached, Is.Null);
            });
        }

        [Test]
        public void TryGetDataSuperset_WhenSupersetFound_ReturnsTrueAndData()
        {
            // Arrange
            MatrixMap requestedMap = new(
            [
                new DimensionMap("dim1", ["val1", "val2"]),
                new DimensionMap("dim2", ["val1", "val2"]),
            ]);

            MatrixMap supersetMap = new(
            [
                new DimensionMap("dim1", ["val1", "val2", "val3"]),
                new DimensionMap("dim2", ["val1", "val2"]),
            ]);

            Task<int[]> supersetData = Task.FromResult<int[]>([ 10, 20, 30, 40, 50, 60 ]);
            object? supersetDataObj = new DataCacheContainer<int>(supersetMap, supersetData);
            MetaCacheContainer metaContainer = new(MatrixMetadata);
            metaContainer.AddDataContainer(new DataCacheContainer<int>(supersetMap, supersetData));
            object? metaContainerObj = metaContainer;

            Mock<IMemoryCache> memoryCacheMock = new();
            memoryCacheMock.Setup(m => m.TryGetValue(It.Is<object>(k => (int)k == MetaCacheUtils.GetCacheKey(_tableId)), out metaContainerObj))
                .Returns(true);
            memoryCacheMock.Setup(m => m.TryGetValue(It.Is<object>(k => k.Equals(MetaCacheUtils.GetCacheKey(supersetMap))), out supersetDataObj))
                .Returns(true);
            DatabaseCache sut = new(memoryCacheMock.Object);

            // Act
            bool result = sut.TryGetDataSuperset(_tableId, requestedMap, out IMatrixMap? resultSuper, out Task<int[]>? resultData, out DateTime? cached);

            // Assert
            Assert.Multiple(async () =>
            {
                Assert.That(result, Is.True);
                Assert.That(resultSuper, Is.EqualTo(supersetMap));
                Assert.That(await resultData!, Is.EqualTo(await supersetData));
                Assert.That(cached, Is.LessThanOrEqualTo(DateTime.UtcNow));
            });
        }

        [Test]
        public void SetData_WhenMetaContainerExists_AddsDataToCache()
        {
            // Arrange
            object? metaContainer = new MetaCacheContainer(MatrixMetadata);
            Mock<IMemoryCache> memoryCacheMock = new();
            memoryCacheMock.Setup(m => m.TryGetValue(It.Is<object>(k => (int)k == MetaCacheUtils.GetCacheKey(_tableId)), out metaContainer))
                .Returns(true);

            Mock<ICacheEntry> cacheDataEntryMock = new();
            cacheDataEntryMock.Setup(c => c.PostEvictionCallbacks)
                .Returns([]);
            object? capturedKey = null;
            ICacheEntry? capturedObject = null;
            memoryCacheMock.Setup(m => m.CreateEntry(It.IsAny<object>()))
                .Callback<object>((key) => capturedKey = key)
                .Returns(() =>
                {
                    cacheDataEntryMock.SetupGet(c => c.Key).Returns(capturedKey!);
                    capturedObject = cacheDataEntryMock.Object;
                    return capturedObject;
                });


            MatrixMap dataMap = new(
            [
                new DimensionMap("dim1", ["val1", "val2"]),
                new DimensionMap("dim2", ["val1", "val2"]),
            ]);

            Task<int[]> testData = Task.FromResult<int[]>([ 1, 2, 3, 4 ]);
            DatabaseCache sut = new(memoryCacheMock.Object);

            // Act
            sut.SetData(_tableId, dataMap, testData);

            // Assert
            memoryCacheMock.Verify(memoryCacheMock => memoryCacheMock.CreateEntry(
                It.Is<int>(k => k == MetaCacheUtils.GetCacheKey(dataMap))),
                Times.Once);

            MetaCacheContainer? containerAfterTest = metaContainer as MetaCacheContainer;
            Assert.Multiple(() =>
            {
                Assert.That(containerAfterTest!.GetRelatedMaps(), Has.Count.EqualTo(1));
                Assert.That(containerAfterTest!.GetRelatedMaps().First(), Is.EqualTo(dataMap));
                Assert.That(capturedObject!.PostEvictionCallbacks, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public void SetData_WhenMetaContainerDoesNotExist_ThrowsInvalidOperationException()
        {
            // Arrange
            Task<int[]> testData = Task.FromResult<int[]>([ 1, 2, 3 ]);
            MatrixMap matrixMap = new([new DimensionMap("foo", ["bar1", "bar2", "bar3"])]);
            object? nullContainer = null;
            MemoryCacheEntryOptions options = new();

            Mock<IMemoryCache> memoryCacheMock = new();
            memoryCacheMock.Setup(m => m.TryGetValue(It.Is<object>(k => (int)k == MetaCacheUtils.GetCacheKey(_tableId)), out nullContainer))
                .Returns(false);
            memoryCacheMock.Setup(m => m.CreateEntry(It.IsAny<object>()))
                .Callback<object>((key) => Assert.Fail($"CreateEntry should not be called for tableId '{_tableId.Id}'"))
                .Throws(new InvalidOperationException("This should not be called."));

            DatabaseCache sut = new(memoryCacheMock.Object);

            // Act & Assert
            Assert.Multiple(() =>
            {
                InvalidOperationException? ex = Assert.Throws<InvalidOperationException>(() => 
                    sut.SetData(_tableId, matrixMap, testData));
                Assert.That(ex?.Message, Does.Contain(_tableId.Id));
            });
        }

        [Test]
        public void SetData_WhenSubmapsExist_RemovesSubmapsFromCache()
        {
            // Arrange
            Task<int[]> testData = Task.FromResult<int[]>([1, 2, 3]);
            MatrixMap matrixMap = new([new DimensionMap("foo", ["bar1", "bar2", "bar3"])]);

            MetaCacheContainer metaContainer = new(MatrixMetadata);
            MatrixMap subMap1 = new([new DimensionMap("foo", ["bar1", "bar2"])]);
            MatrixMap subMap2 = new([new DimensionMap("foo", ["bar2", "bar3"])]);
            MatrixMap differentMap = new([new DimensionMap("foo", ["notbar1", "notbar2"])]);
            DataCacheContainer<int> subContainer1 = new(subMap1, Task.FromResult<int[]>([1, 2]));
            DataCacheContainer<int> subContainer2 = new(subMap2, Task.FromResult<int[]>([2, 3]));
            metaContainer.AddDataContainer(subContainer1);
            metaContainer.AddDataContainer(subContainer2);
            metaContainer.AddDataContainer(new DataCacheContainer<int>(differentMap, Task.FromResult<int[]>([8, 9])));

            Mock<IMemoryCache> memoryCacheMock = new();
            object? metaContainerObj = metaContainer;

            memoryCacheMock.Setup(m => m.TryGetValue(It.Is<int>(k => k == MetaCacheUtils.GetCacheKey(_tableId)), out metaContainerObj))
                .Returns(true);
            // The mock implementation does not invoke eviction callbacks automatically.
            memoryCacheMock.Setup(m => m.Remove(It.IsAny<object>()))
                .Callback<object>(key =>
                {
                    if (key is int iKey)
                    {
                        if (iKey == MetaCacheUtils.GetCacheKey(subMap1))
                        {
                            subContainer1.EvictionCallback(key, null, EvictionReason.Removed, null);
                        }
                        else if (iKey == MetaCacheUtils.GetCacheKey(subMap2))
                        {
                            subContainer2.EvictionCallback(key, null, EvictionReason.Removed, null);
                        }
                        else
                        {
                            Assert.Fail("Remove called for a wrong map!");
                        }
                    }
                    else
                    {
                        Assert.Fail("Remove called with an invalid key!");
                    }
                });

            Mock<ICacheEntry> cacheEntryMock = new();
            cacheEntryMock.Setup(c => c.PostEvictionCallbacks)
                .Returns([]);
            memoryCacheMock.Setup(m => m.CreateEntry(It.IsAny<object>()))
                .Returns(() => cacheEntryMock.Object);

            DatabaseCache sut = new(memoryCacheMock.Object);

            // Act
            sut.SetData(_tableId, matrixMap, testData);

            // Assert
            memoryCacheMock.Verify(m => m.Remove(It.IsAny<object>()), Times.Exactly(2));
            Assert.That(metaContainer.GetRelatedMaps(), Has.Count.EqualTo(2));
            Assert.That(metaContainer.GetRelatedMaps(), Does.Contain(differentMap));
        }

        [Test]
        public void OnMetaCacheEvicted_RemovesRelatedMapsFromCache()
        {
            // Arrange
            Dictionary<int, ICacheEntry> cacheEntries = [];
            object? metaContainer = new MetaCacheContainer(MatrixMetadata);
            Mock<IMemoryCache> memoryCacheMock = new();

            // Capture the callback to simulate eviction
            memoryCacheMock.Setup(m => m.CreateEntry(It.IsAny<int>()))
                .Returns<object>(key =>
                {
                    Mock<ICacheEntry> mockEntry = new();
                    mockEntry.Setup(m => m.PostEvictionCallbacks).Returns([]);
                    ICacheEntry entry = mockEntry.Object;
                    cacheEntries[(int)key] = entry;
                    return entry;
                });

            memoryCacheMock.Setup(m => m.TryGetValue(It.Is<object>(k => (int)k == MetaCacheUtils.GetCacheKey(_tableId)), out metaContainer))
                .Returns(true);

            memoryCacheMock.Setup(m => m.Remove(It.IsAny<int>()))
                .Callback<object>(key =>
                {
                    IList<PostEvictionCallbackRegistration> callbacks = cacheEntries[(int)key].PostEvictionCallbacks;
                    cacheEntries.Remove((int)key);
                    foreach (PostEvictionDelegate? callback in callbacks.Select(cbr => cbr.EvictionCallback))
                    {
                        object? value = (int)key == MetaCacheUtils.GetCacheKey(_tableId) ? metaContainer! : null;
                        callback?.Invoke(key, value, EvictionReason.Removed, null);
                    }
                });

            MemoryCacheEntryOptions options = new();
            IMemoryCache cache = memoryCacheMock.Object;
            DatabaseCache sut = new(cache);

            // Act
            sut.SetMetadata(_tableId, (MetaCacheContainer)metaContainer);
            sut.SetData(_tableId, new([new DimensionMap("foo", ["bar1", "bar2"])]), Task.FromResult<int[]>([1, 2]));
            sut.SetData(_tableId, new([new DimensionMap("foo", ["bar2", "bar3"])]), Task.FromResult<int[]>([2, 3]));
            cache.Remove(MetaCacheUtils.GetCacheKey(_tableId));

            // Assert
            memoryCacheMock.Verify(m => m.Remove(It.IsAny<object>()), Times.AtLeast(3));
        }

        [Test]
        public void TryGetDataSuperset_WhenSupersetFound_ReturnsTrueAndSupersetMap()
        {
            // Arrange
            object? metaContainer = new MetaCacheContainer(MatrixMetadata);
            Mock<IMemoryCache> memoryCacheMock = new();

            // Capture the callback to simulate eviction
            memoryCacheMock.Setup(m => m.CreateEntry(It.IsAny<int>()))
                .Returns<object>(key =>
                {
                    Mock<ICacheEntry> mockEntry = new();
                    mockEntry.Setup(m => m.PostEvictionCallbacks).Returns([]);
                    return mockEntry.Object;
                });

            memoryCacheMock.Setup(m => m.TryGetValue(It.Is<int>(k => k == MetaCacheUtils.GetCacheKey(_tableId)), out metaContainer))
                .Returns(true);

            MemoryCacheEntryOptions options = new();
            MatrixMap superMap = new([new DimensionMap("foo", ["bar1", "bar2", "bar3"])]);
            Task<int[]> superData = Task.FromResult<int[]>([1, 2, 3]);
            object? superContainer = new DataCacheContainer<int>(superMap, superData);

            memoryCacheMock.Setup(m => m.TryGetValue(It.Is<object>(k => (int)k == MetaCacheUtils.GetCacheKey(superMap)), out superContainer))
                .Returns(true);

            DatabaseCache sut = new(memoryCacheMock.Object);

            // Act
            sut.SetMetadata(_tableId, (MetaCacheContainer)metaContainer);
            sut.SetData(_tableId, superMap, Task.FromResult<int[]>([1, 2, 3]));

            bool result = sut.TryGetDataSuperset(
                _tableId,
                new MatrixMap([new DimensionMap("foo", ["bar1", "bar2"])]),
                out IMatrixMap? resultMap,
                out Task<int[]>? resultData,
                out DateTime? cached);

            // Assert
            Assert.Multiple(async () =>
            {
                Assert.That(result, Is.True);
                Assert.That(resultMap, Is.EqualTo(superMap));
                Assert.That(await resultData!, Is.EqualTo(await superData));
            });
        }
    }
}