using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using PxApi.Caching;
using PxApi.Configuration;
using PxApi.Controllers;
using PxApi.Models;
using System.Collections.Immutable;
using PxApi.UnitTests.Utils; // Added for TestConfigFactory

namespace PxApi.UnitTests.ControllerTests
{
    [TestFixture]
    public class CacheControllerTests
    {
        private Mock<ICachedDataSource> _cachedDbConnector = null!;
        private Mock<ILogger<CacheController>> _mockLogger = null!;
        private CacheController _controller = null!;

        [SetUp]
        public void SetUp()
        {
            _cachedDbConnector = new Mock<ICachedDataSource>();
            _mockLogger = new Mock<ILogger<CacheController>>();
            _controller = new CacheController(_cachedDbConnector.Object, _mockLogger.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };

            // Use TestConfigFactory to build configuration with localization and mounted database settings
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                TestConfigFactory.MountedDb(0, "testdb", "datasource/root/"),
                new Dictionary<string, string?>
                {
                    ["DataBases:0:CacheConfig:Modifiedtime:SlidingExpirationSeconds"] = "60",
                    ["DataBases:0:CacheConfig:Modifiedtime:AbsoluteExpirationSeconds"] = "60"
                }
            );
            IConfiguration configuration = TestConfigFactory.BuildAndLoad(configData);
        }

        [Test]
        public async Task ClearTableCache_DatabaseExists_CallsClearTableCacheMethod_ReturnsOk()
        {
            // Arrange
            const string database = "testdb";
            const string id = "table1";
            DataBaseRef dbRef = DataBaseRef.Create(database);
            PxFileRef fileRef = PxFileRef.CreateFromPath(Path.Combine("c:", "foo", id), dbRef);
            _cachedDbConnector.Setup(x => x.GetDataBaseReference(database)).Returns(dbRef);
            _cachedDbConnector.Setup(x => x.GetFileListCachedAsync(dbRef))
                .ReturnsAsync(ImmutableSortedDictionary<string, PxFileRef>.Empty.Add(id, fileRef));
            _cachedDbConnector.Setup(x => x.ClearTableCache(fileRef));

            // Act
            ActionResult result = await _controller.ClearTableCacheAsync(database, id);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            _cachedDbConnector.Verify(x => x.ClearTableCache(fileRef), Times.Once);
        }

        [Test]
        public async Task ClearTableCache_DatabaseDoesNotExist_ReturnsNotFound()
        {
            // Arrange
            const string database = "nonexistentdb";
            const string id = "table1";
            _cachedDbConnector.Setup(x => x.GetDataBaseReference(database)).Returns((DataBaseRef?)null);

            // Act
            ActionResult result = await _controller.ClearTableCacheAsync(database, id);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
            _cachedDbConnector.Verify(x => x.ClearTableCache(It.IsAny<PxFileRef>()), Times.Never);
        }

        [Test]
        public async Task ClearTableCache_FileDoesNotExist_ReturnsNotFound()
        {
            // Arrange
            const string database = "testdb";
            const string id = "nonexistentfile";
            DataBaseRef dbRef = DataBaseRef.Create(database);
            _cachedDbConnector.Setup(x => x.GetDataBaseReference(database)).Returns(dbRef);
            _cachedDbConnector.Setup(x => x.GetFileListCachedAsync(dbRef))
                .ReturnsAsync(ImmutableSortedDictionary<string, PxFileRef>.Empty);

            // Act
            ActionResult result = await _controller.ClearTableCacheAsync(database, id);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
            _cachedDbConnector.Verify(x => x.ClearTableCache(It.IsAny<PxFileRef>()), Times.Never);
        }

        [Test]
        public async Task ClearTableCache_ExceptionThrown_ReturnsStatusCode500()
        {
            // Arrange
            const string database = "testdb";
            const string id = "table1";
            DataBaseRef dbRef = DataBaseRef.Create(database);
            _cachedDbConnector.Setup(x => x.GetDataBaseReference(database)).Returns(dbRef);
            _cachedDbConnector.Setup(x => x.GetFileListCachedAsync(dbRef))
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            ActionResult result = await _controller.ClearTableCacheAsync(database, id);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            ObjectResult? statusCodeResult = result as ObjectResult;
            Assert.That(statusCodeResult, Is.Not.Null);
            Assert.That(statusCodeResult!.StatusCode, Is.EqualTo(500));
        }

        [Test]
        public async Task ClearAllCache_DatabaseExists_CallsClearDatabaseCacheAsyncMethod_ReturnsOk()
        {
            // Arrange
            const string database = "testdb";
            DataBaseRef dbRef = DataBaseRef.Create(database);
            _cachedDbConnector.Setup(x => x.GetDataBaseReference(database)).Returns(dbRef);
            _cachedDbConnector.Setup(x => x.ClearDatabaseCacheAsync(dbRef));

            // Act
            ActionResult result = await _controller.ClearAllCacheAsync(database);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            _cachedDbConnector.Verify(x => x.ClearDatabaseCacheAsync(dbRef), Times.Once);
        }

        [Test]
        public async Task ClearAllCache_DatabaseDoesNotExist_ReturnsNotFound()
        {
            // Arrange
            const string database = "nonexistentdb";
            _cachedDbConnector.Setup(x => x.GetDataBaseReference(database)).Returns((DataBaseRef?)null);

            // Act
            ActionResult result = await _controller.ClearAllCacheAsync(database);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
            _cachedDbConnector.Verify(x => x.ClearDatabaseCacheAsync(It.IsAny<DataBaseRef>()), Times.Never);
        }

        [Test]
        public async Task ClearAllCache_ExceptionThrown_ReturnsStatusCode500()
        {
            // Arrange
            DataBaseRef dbRef = DataBaseRef.Create("testdb");
            _cachedDbConnector.Setup(x => x.GetDataBaseReference("testdb")).Returns(dbRef);
            _cachedDbConnector.Setup(x => x.ClearDatabaseCacheAsync(dbRef)).Throws(new Exception("Test exception"));

            // Act
            ActionResult result = await _controller.ClearAllCacheAsync("testdb");

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            ObjectResult? statusCodeResult = result as ObjectResult;
            Assert.That(statusCodeResult, Is.Not.Null);
            Assert.That(statusCodeResult!.StatusCode, Is.EqualTo(500));
        }
    }
}