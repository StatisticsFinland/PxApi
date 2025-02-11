using PxApi.Caching;

namespace PxApi.UnitTests.CachingTests
{
    internal static class CacheItemTests
    {
        [Test]
        public static void CacheItemConstructorTest()
        {
            // Arrange
            Task<string> task = Task.FromResult("Test");
            TimeSpan staysFresh = TimeSpan.FromSeconds(5);
            DateTime fileModified = DateTime.Now;
            // Act
            CacheItem<string> cacheItem = new(task, staysFresh, fileModified);
            Assert.Multiple(() =>
            {
                // Assert
                Assert.That(cacheItem.Task, Is.EqualTo(task));
                Assert.That(cacheItem.StaysFresh, Is.EqualTo(staysFresh));
                Assert.That(cacheItem.FileModified, Is.EqualTo(fileModified));
            });
        }

        [Test]
        public static void CacheItemCopyConstructorTest()
        {
            // Arrange
            Task<string> task = Task.FromResult("Test");
            TimeSpan staysFresh = TimeSpan.FromSeconds(5);
            DateTime fileModified = DateTime.Now;
            CacheItem<string> original = new(task, staysFresh, fileModified);
            // Act
            CacheItem<string> cacheItem = new(original);
            Assert.Multiple(() =>
            {
                // Assert
                Assert.That(cacheItem.Task, Is.EqualTo(task));
                Assert.That(cacheItem.StaysFresh, Is.EqualTo(staysFresh));
                Assert.That(cacheItem.FileModified, Is.EqualTo(fileModified));
            });
        }

        [Test]
        public static void CacheItemIsFreshTest()
        {
            // Arrange
            Task<string> task = Task.FromResult("Test");
            TimeSpan staysFresh = TimeSpan.FromSeconds(5);
            DateTime fileModified = DateTime.Now;
            CacheItem<string> cacheItem = new(task, staysFresh, fileModified);

            // Assert
            Assert.That(cacheItem.IsFresh, Is.True);
        }

        [Test]
        public static void CacheItemIsNotFreshTest()
        {
            // Arrange
            Task<string> task = Task.FromResult("Test");
            TimeSpan staysFresh = TimeSpan.FromMilliseconds(1);
            DateTime fileModified = DateTime.Now;
            CacheItem<string> cacheItem = new(task, staysFresh, fileModified);
            Task.Delay(2).Wait();

            // Assert
            Assert.That(cacheItem.IsFresh, Is.False);
        }
    }
}
