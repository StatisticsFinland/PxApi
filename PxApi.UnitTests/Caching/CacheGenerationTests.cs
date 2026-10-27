using Microsoft.Extensions.Caching.Memory;
using Moq;
using Px.Utils.Models.Metadata;
using PxApi.Caching;
using PxApi.Models;
using PxApi.UnitTests.Utils;
using System.Collections.Concurrent;

namespace PxApi.UnitTests.Caching
{
    [TestFixture]
    internal class CacheGenerationTests
    {
        private readonly PxFileRef _file = PxFileRef.ValidateAndCreate("table", DataBaseRef.Create("TestDb"));
        private readonly ConcurrentDictionary<object, ICacheEntry> _entries = new();
        private Mock<IMemoryCache> _memory = null!;
        private DatabaseCache _cache = null!;
        private Action<ICacheEntry>? _beforePublish;
        private object? _rejectedKey;

        [SetUp]
        public void SetUp()
        {
            TestConfigFactory.BuildAndLoad(TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                TestConfigFactory.MountedDb(0, _file.DataBase.Id)));
            _entries.Clear();
            _beforePublish = null;
            _rejectedKey = null;
            _memory = new Mock<IMemoryCache>();
            _memory.Setup(cache => cache.TryGetValue(It.IsAny<object>(), out It.Ref<object?>.IsAny))
                .Returns((object key, out object? value) =>
                {
                    bool found = _entries.TryGetValue(key, out ICacheEntry? entry);
                    value = entry?.Value;
                    return found;
                });
            _memory.Setup(cache => cache.Remove(It.IsAny<object>()))
                .Callback<object>(key => { _entries.TryRemove(key, out _); });
            _memory.Setup(cache => cache.CreateEntry(It.IsAny<object>()))
                .Returns<object>(key =>
                {
                    Mock<ICacheEntry> entry = new();
                    entry.SetupAllProperties();
                    entry.SetupGet(item => item.Key).Returns(key);
                    entry.SetupGet(item => item.PostEvictionCallbacks).Returns([]);
                    entry.Setup(item => item.Dispose()).Callback(() =>
                    {
                        _beforePublish?.Invoke(entry.Object);
                        if (key.Equals(_rejectedKey))
                        {
                            RunEviction(entry.Object, EvictionReason.Capacity);
                        }
                        else
                        {
                            _entries[key] = entry.Object;
                        }
                    });
                    return entry.Object;
                });
            // Callbacks are invoked explicitly to reproduce ordering without sleeps or thread-pool timing.
            _cache = new DatabaseCache(_memory.Object);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MetadataRemovedOrReplaced_BeforeCallback_HidesOldData(bool replace)
        {
            MatrixMap map = new([new DimensionMap("dimension", ["one", "two"])]);
            MatrixMap subset = new([new DimensionMap("dimension", ["one"])]);
            MetaCacheContainer oldMetadata = CreateMetadata();
            _cache.SetMetadata(_file, oldMetadata);
            _cache.SetData(_file, oldMetadata, map, Task.FromResult<int[]>([1, 2]));
            ICacheEntry oldMetadataEntry = _entries[MetaCacheUtils.GetCacheKey(_file)];
            object oldDataKey = MetaCacheUtils.GetCacheKey(_file, oldMetadata, map);

            _cache.TryRemoveMeta(_file);
            if (replace)
            {
                _cache.SetMetadata(_file, CreateMetadata());
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(_entries.ContainsKey(oldDataKey), Is.True);
                Assert.That(_cache.TryGetData<int>(_file, map, out _, out _), Is.False);
                Assert.That(_cache.TryGetDataSuperset<int>(_file, subset, out _, out _, out _), Is.False);
            }

            RunEviction(oldMetadataEntry);

            Assert.That(_entries.ContainsKey(oldDataKey), Is.False);
        }

        [TestCase(EvictionReason.Removed)]
        [TestCase(EvictionReason.Replaced)]
        [TestCase(EvictionReason.Expired)]
        [TestCase(EvictionReason.Capacity)]
        public void OldMetadataEviction_PreservesReplacementGeneration(EvictionReason reason)
        {
            MatrixMap map = new([new DimensionMap("dimension", ["one", "two"])]);
            MatrixMap subset = new([new DimensionMap("dimension", ["two"])]);
            Task<IReadOnlyMatrixMetadata> metadata = Task.FromResult(Mock.Of<IReadOnlyMatrixMetadata>());
            MetaCacheContainer oldMetadata = new(metadata);
            MetaCacheContainer newMetadata = new(metadata);
            _cache.SetMetadata(_file, oldMetadata);
            _cache.SetData(_file, oldMetadata, map, Task.FromResult<int[]>([1, 2]));
            ICacheEntry oldMetadataEntry = _entries[MetaCacheUtils.GetCacheKey(_file)];
            object oldDataKey = MetaCacheUtils.GetCacheKey(_file, oldMetadata, map);
            Task<int[]> newData = Task.FromResult<int[]>([3, 4]);
            _cache.SetMetadata(_file, newMetadata);
            _cache.SetData(_file, newMetadata, map, newData);

            RunEviction(oldMetadataEntry, reason);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(_entries.ContainsKey(oldDataKey), Is.False);
                Assert.That(_cache.TryGetData(_file, map, out Task<int[]>? exact, out _), Is.True);
                Assert.That(exact, Is.SameAs(newData));
                Assert.That(_cache.TryGetDataSuperset(_file, subset, out _, out Task<int[]>? superset, out _), Is.True);
                Assert.That(superset, Is.SameAs(newData));
                Assert.That(newMetadata.GetRelatedMaps(), Is.EqualTo(new[] { map }));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SetData_MetadataChangesAfterCheck_OldWriteIsIsolatedAndCleaned(bool replace)
        {
            MatrixMap map = new([new DimensionMap("dimension", ["one", "two"])]);
            MatrixMap subset = new([new DimensionMap("dimension", ["one"])]);
            MetaCacheContainer oldMetadata = CreateMetadata();
            MetaCacheContainer newMetadata = CreateMetadata();
            _cache.SetMetadata(_file, oldMetadata);
            ICacheEntry oldMetadataEntry = _entries[MetaCacheUtils.GetCacheKey(_file)];
            object oldDataKey = MetaCacheUtils.GetCacheKey(_file, oldMetadata, map);
            Task<int[]> newData = Task.FromResult<int[]>([3, 4]);
            _beforePublish = entry =>
            {
                if (!entry.Key.Equals(oldDataKey)) return;
                _cache.TryRemoveMeta(_file);
                if (replace)
                {
                    _cache.SetMetadata(_file, newMetadata);
                    _cache.SetData(_file, newMetadata, map, newData);
                }
            };

            _cache.SetData(_file, oldMetadata, map, Task.FromResult<int[]>([1, 2]));

            Assert.That(_entries.ContainsKey(oldDataKey), Is.True);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(_cache.TryGetData(_file, map, out Task<int[]>? exact, out _), Is.EqualTo(replace));
                Assert.That(exact, replace ? Is.SameAs(newData) : Is.Null);
                Assert.That(_cache.TryGetDataSuperset(_file, subset, out _, out Task<int[]>? superset, out _), Is.EqualTo(replace));
                Assert.That(superset, replace ? Is.SameAs(newData) : Is.Null);
            }

            RunEviction(oldMetadataEntry);

            Assert.That(_entries.ContainsKey(oldDataKey), Is.False);
            if (replace)
            {
                Assert.That(_cache.TryGetData(_file, map, out Task<int[]>? retained, out _), Is.True);
                Assert.That(retained, Is.SameAs(newData));
            }
        }

        [Test]
        public async Task SetData_ConcurrentMetadataCleanup_RemovesPublishedOldData()
        {
            MatrixMap map = new([new DimensionMap("dimension", ["one"])]);
            MetaCacheContainer metadata = CreateMetadata();
            _cache.SetMetadata(_file, metadata);
            ICacheEntry metadataEntry = _entries[MetaCacheUtils.GetCacheKey(_file)];
            object dataKey = MetaCacheUtils.GetCacheKey(_file, metadata, map);
            using ManualResetEventSlim cleanupStarted = new();
            Task? cleanup = null;
            bool dataRemoved = false;
            _memory.Setup(cache => cache.Remove(It.IsAny<object>()))
                .Callback<object>(key =>
                {
                    if (key.Equals(dataKey))
                    {
                        Assert.That(metadata.DataAccessLock.IsHeldByCurrentThread, Is.True,
                            "Metadata cleanup must hold the generation lock while removing data.");
                        Assert.That(_entries.ContainsKey(dataKey), Is.True,
                            "Cleanup must wait until the overlapping publication has committed.");
                        dataRemoved = true;
                    }
                    _entries.TryRemove(key, out _);
                });
            _beforePublish = entry =>
            {
                if (!entry.Key.Equals(dataKey)) return;
                Assert.That(metadata.DataAccessLock.IsHeldByCurrentThread, Is.True,
                    "Publication must hold the same generation lock as metadata cleanup.");
                _cache.TryRemoveMeta(_file);
                cleanup = Task.Run(() =>
                {
                    cleanupStarted.Set();
                    RunEviction(metadataEntry);
                });
                Assert.That(cleanupStarted.Wait(TimeSpan.FromSeconds(5)), Is.True);
            };

            try
            {
                _cache.SetData(_file, metadata, map, Task.FromResult<int[]>([1]));
            }
            finally
            {
                if (cleanup is not null)
                {
                    await cleanup.WaitAsync(TimeSpan.FromSeconds(5));
                }
            }
            Assert.That(cleanup, Is.Not.Null);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(dataRemoved, Is.True);
                Assert.That(_entries.ContainsKey(dataKey), Is.False);
                Assert.That(_cache.TryGetData<int>(_file, map, out _, out _), Is.False);
            }
        }

        [Test]
        public async Task SetData_MetadataCleanupAlreadyInProgress_SkipsRetiredGeneration()
        {
            MatrixMap map = new([new DimensionMap("dimension", ["one"])]);
            MetaCacheContainer metadata = CreateMetadata();
            Mock<ICacheObserver> observer = new();
            _cache = new DatabaseCache(_memory.Object, observer.Object);
            _cache.SetMetadata(_file, metadata);
            _cache.SetData(_file, metadata, map, Task.FromResult<int[]>([1]));
            ICacheEntry metadataEntry = _entries[MetaCacheUtils.GetCacheKey(_file)];
            object dataKey = MetaCacheUtils.GetCacheKey(_file, metadata, map);
            using ManualResetEventSlim cleanupAtRemoval = new();
            using ManualResetEventSlim finishCleanup = new();
            using ManualResetEventSlim writerStarted = new();
            observer.Setup(item => item.RecordDataCacheWriteSkipped())
                .Callback(() => Assert.That(metadata.DataAccessLock.IsHeldByCurrentThread, Is.True,
                    "The writer must check the retired generation under the lifecycle lock."));
            _memory.Setup(cache => cache.Remove(It.IsAny<object>()))
                .Callback<object>(key =>
                {
                    if (key.Equals(dataKey))
                    {
                        Assert.That(metadata.DataAccessLock.IsHeldByCurrentThread, Is.True,
                            "Metadata cleanup must hold the generation lock while removing data.");
                        cleanupAtRemoval.Set();
                        Assert.That(finishCleanup.Wait(TimeSpan.FromSeconds(5)), Is.True);
                    }
                    _entries.TryRemove(key, out _);
                });
            _beforePublish = entry =>
            {
                if (entry.Key.Equals(dataKey))
                {
                    Assert.Fail("A writer must not publish after its metadata generation has been retired.");
                }
            };
            _cache.TryRemoveMeta(_file);
            Task cleanup = Task.Run(() => RunEviction(metadataEntry));
            Task? writer = null;

            try
            {
                Assert.That(cleanupAtRemoval.Wait(TimeSpan.FromSeconds(5)), Is.True);
                writer = Task.Run(() =>
                {
                    writerStarted.Set();
                    _cache.SetData(_file, metadata, map, Task.FromResult<int[]>([2]));
                });
                Assert.That(writerStarted.Wait(TimeSpan.FromSeconds(5)), Is.True);
            }
            finally
            {
                finishCleanup.Set();
                await Task.WhenAll(cleanup, writer ?? Task.CompletedTask).WaitAsync(TimeSpan.FromSeconds(5));
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(_entries.ContainsKey(dataKey), Is.False);
                Assert.That(_cache.TryGetData<int>(_file, map, out _, out _), Is.False);
            }
            observer.Verify(item => item.RecordDataCacheWriteSkipped(), Times.Once);
            observer.VerifyNoOtherCalls();
        }

        [Test]
        public void OldDataEviction_PreservesReplacementRegistrationAndLaterCleanup()
        {
            MatrixMap map = new([new DimensionMap("dimension", ["one", "two"])]);
            MatrixMap replacementMap = new([new DimensionMap("dimension", ["one", "two"])]);
            MatrixMap subset = new([new DimensionMap("dimension", ["one"])]);
            MetaCacheContainer metadata = CreateMetadata();
            _cache.SetMetadata(_file, metadata);
            ICacheEntry metadataEntry = _entries[MetaCacheUtils.GetCacheKey(_file)];
            _cache.SetData(_file, metadata, map, Task.FromResult<int[]>([1, 2]));
            object dataKey = MetaCacheUtils.GetCacheKey(_file, metadata, map);
            ICacheEntry oldDataEntry = _entries[dataKey];
            Task<int[]> replacement = Task.FromResult<int[]>([3, 4]);
            _cache.SetData(_file, metadata, replacementMap, replacement);

            RunEviction(oldDataEntry, EvictionReason.Replaced);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(metadata.GetRelatedMaps(), Has.Count.EqualTo(1));
                Assert.That(metadata.GetRelatedMaps()[0], Is.SameAs(replacementMap));
                Assert.That(_cache.TryGetDataSuperset(_file, subset, out _, out Task<int[]>? data, out _), Is.True);
                Assert.That(data, Is.SameAs(replacement));
            }

            _cache.TryRemoveMeta(_file);
            RunEviction(metadataEntry);

            Assert.That(_entries.ContainsKey(dataKey), Is.False);
        }

        [Test]
        public void SetData_ImmediateCapacityRejection_DoesNotLeaveRegistration()
        {
            MatrixMap map = new([new DimensionMap("dimension", ["one"])]);
            MetaCacheContainer metadata = CreateMetadata();
            _cache.SetMetadata(_file, metadata);
            _rejectedKey = MetaCacheUtils.GetCacheKey(_file, metadata, map);

            _cache.SetData(_file, metadata, map, Task.FromResult<int[]>([1]));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(metadata.GetRelatedMaps(), Is.Empty);
                Assert.That(_cache.TryGetData<int>(_file, map, out _, out _), Is.False);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OldDataFailureOrCancellation_PreservesReplacementGeneration(bool cancel)
        {
            MatrixMap map = new([new DimensionMap("dimension", ["one"])]);
            MetaCacheContainer oldMetadata = CreateMetadata();
            MetaCacheContainer newMetadata = CreateMetadata();
            TaskCompletionSource<int[]> oldData = new();
            _cache.SetMetadata(_file, oldMetadata);
            _cache.SetData(_file, oldMetadata, map, oldData.Task);
            ICacheEntry oldDataEntry = _entries[MetaCacheUtils.GetCacheKey(_file, oldMetadata, map)];
            _cache.SetMetadata(_file, newMetadata);
            Task<int[]> newData = Task.FromResult<int[]>([2]);
            _cache.SetData(_file, newMetadata, map, newData);

            if (cancel)
            {
                oldData.SetCanceled();
            }
            else
            {
                oldData.SetException(new InvalidOperationException("Old data failed."));
            }
            RunEviction(oldDataEntry);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(_cache.TryGetData(_file, map, out Task<int[]>? actual, out _), Is.True);
                Assert.That(actual, Is.SameAs(newData));
                Assert.That(newMetadata.GetRelatedMaps(), Is.EqualTo(new[] { map }));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OldDataFailureOrCancellation_PreservesReplacementInSameGeneration(bool cancel)
        {
            MatrixMap map = new([new DimensionMap("dimension", ["one", "two"])]);
            MatrixMap subset = new([new DimensionMap("dimension", ["one"])]);
            MetaCacheContainer metadata = CreateMetadata();
            TaskCompletionSource<int[]> oldData = new();
            _cache.SetMetadata(_file, metadata);
            _cache.SetData(_file, metadata, map, oldData.Task);
            ICacheEntry oldDataEntry = _entries[MetaCacheUtils.GetCacheKey(_file, metadata, map)];
            Task<int[]> replacement = Task.FromResult<int[]>([3, 4]);
            _cache.SetData(_file, metadata, map, replacement);

            if (cancel)
            {
                oldData.SetCanceled();
            }
            else
            {
                oldData.SetException(new InvalidOperationException("Replaced data failed."));
            }
            RunEviction(oldDataEntry);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(_cache.TryGetData(_file, map, out Task<int[]>? exact, out _), Is.True);
                Assert.That(exact, Is.SameAs(replacement));
                Assert.That(_cache.TryGetDataSuperset(_file, subset, out _, out Task<int[]>? superset, out _), Is.True);
                Assert.That(superset, Is.SameAs(replacement));
                Assert.That(metadata.GetRelatedMaps(), Is.EqualTo(new[] { map }));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task OldDataFailureOrCancellation_OverlapsReplacement_SerializesCleanup(bool cancel)
        {
            MatrixMap map = new([new DimensionMap("dimension", ["one", "two"])]);
            MatrixMap subset = new([new DimensionMap("dimension", ["one"])]);
            MetaCacheContainer metadata = CreateMetadata();
            TaskCompletionSource<int[]> oldData = new();
            _cache.SetMetadata(_file, metadata);
            _cache.SetData(_file, metadata, map, oldData.Task);
            object dataKey = MetaCacheUtils.GetCacheKey(_file, metadata, map);
            ICacheEntry oldDataEntry = _entries[dataKey];
            Task<int[]> replacement = Task.FromResult<int[]>([3, 4]);
            using ManualResetEventSlim lookupReached = new();
            using ManualResetEventSlim resumeCleanup = new();
            using ManualResetEventSlim writerStarted = new();
            using ManualResetEventSlim removalFinished = new();
            Thread? cleanupThread = null;
            bool lookupLockHeld = false;
            bool removalLockHeld = false;
            bool lookupReleased = false;
            bool removedOldEntry = false;
            bool publishedAfterRemoval = false;
            _memory.Setup(cache => cache.TryGetValue(
                    It.Is<object>(key => key.Equals(dataKey)), out It.Ref<object?>.IsAny))
                .Returns((object key, out object? value) =>
                {
                    bool found = _entries.TryGetValue(key, out ICacheEntry? entry);
                    value = entry?.Value;
                    if (!lookupReached.IsSet)
                    {
                        cleanupThread = Thread.CurrentThread;
                        lookupLockHeld = metadata.DataAccessLock.IsHeldByCurrentThread;
                        lookupReached.Set();
                        lookupReleased = resumeCleanup.Wait(TimeSpan.FromSeconds(5));
                    }
                    return found;
                });
            _memory.Setup(cache => cache.Remove(It.IsAny<object>()))
                .Callback<object>(key =>
                {
                    if (key.Equals(dataKey) &&
                        ReferenceEquals(Thread.CurrentThread, cleanupThread) &&
                        !removalFinished.IsSet)
                    {
                        removalLockHeld = metadata.DataAccessLock.IsHeldByCurrentThread;
                        removedOldEntry = _entries.TryRemove(key, out ICacheEntry? entry) &&
                            ReferenceEquals(entry, oldDataEntry);
                        removalFinished.Set();
                    }
                    else
                    {
                        _entries.TryRemove(key, out _);
                    }
                });
            _beforePublish = entry =>
            {
                if (entry.Key.Equals(dataKey))
                {
                    publishedAfterRemoval = removalFinished.IsSet;
                }
            };
            Task cleanup = Task.Run(() =>
            {
                if (cancel)
                {
                    oldData.SetCanceled();
                }
                else
                {
                    oldData.SetException(new InvalidOperationException("Old data failed during replacement."));
                }
            });
            Task? writer = null;

            try
            {
                Assert.That(lookupReached.Wait(TimeSpan.FromSeconds(5)), Is.True);
                writer = Task.Run(() =>
                {
                    writerStarted.Set();
                    _cache.SetData(_file, metadata, map, replacement);
                });
                Assert.That(writerStarted.Wait(TimeSpan.FromSeconds(5)), Is.True);
            }
            finally
            {
                resumeCleanup.Set();
                await Task.WhenAll(cleanup, writer ?? Task.CompletedTask).WaitAsync(TimeSpan.FromSeconds(5));
            }

            Assert.That(removalFinished.Wait(TimeSpan.FromSeconds(5)), Is.True);
            RunEviction(oldDataEntry);

            // Continuation exceptions are not awaited by DatabaseCache, so assert observations on the test thread.
            using (Assert.EnterMultipleScope())
            {
                Assert.That(lookupLockHeld, Is.True, "Failed-load lookup must hold the generation lock.");
                Assert.That(removalLockHeld, Is.True, "Failed-load removal must hold the same generation lock.");
                Assert.That(lookupReleased, Is.True);
                Assert.That(removedOldEntry, Is.True, "Cleanup must remove the old entry, not its replacement.");
                Assert.That(publishedAfterRemoval, Is.True, "Replacement must publish after failed-load cleanup.");
                Assert.That(_cache.TryGetData(_file, map, out Task<int[]>? exact, out _), Is.True);
                Assert.That(exact, Is.SameAs(replacement));
                Assert.That(_cache.TryGetDataSuperset(_file, subset, out _, out Task<int[]>? superset, out _), Is.True);
                Assert.That(superset, Is.SameAs(replacement));
                Assert.That(metadata.GetRelatedMaps(), Is.EqualTo(new[] { map }));
            }
        }

        [Test]
        public void AddDataContainer_RepeatedRegistration_TracksContainerOnce()
        {
            MatrixMap map = new([new DimensionMap("dimension", ["one"])]);
            MetaCacheContainer metadata = CreateMetadata();
            DataCacheContainer<int> data = new(map, Task.FromResult<int[]>([1]));

            metadata.AddDataContainer(data);
            metadata.AddDataContainer(data);
            Assert.That(metadata.GetRelatedMaps(), Has.Count.EqualTo(1));
            data.EvictionCallback(null, null, EvictionReason.Removed, null);

            Assert.That(metadata.GetRelatedMaps(), Is.Empty);
        }

        private static MetaCacheContainer CreateMetadata() =>
            new(Task.FromResult(Mock.Of<IReadOnlyMatrixMetadata>()));

        private static void RunEviction(ICacheEntry entry, EvictionReason reason = EvictionReason.Removed)
        {
            foreach (PostEvictionCallbackRegistration callback in entry.PostEvictionCallbacks)
            {
                callback.EvictionCallback!(entry.Key, entry.Value, reason, callback.State);
            }
        }
    }
}
