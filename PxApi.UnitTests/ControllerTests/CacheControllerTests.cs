using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using PxApi.Caching;
using PxApi.Controllers;
using PxApi.Models;
using System.Collections.Immutable;

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
        }

        [Test]
        public async Task ClearTableCache_DatabaseExists_CallsClearTableCacheMethod_ReturnsOk()
        {
            // Arrange
            const string database = "testdb";
            const string id = "table1";
            DataBaseRef dbRef = DataBaseRef.Create(database);
            PxFileRef fileRef = PxFileRef.ValidateAndCreate(id, dbRef, ["statisticalProgram"]);
            _cachedDbConnector.Setup(x => x.GetDataBaseReference(database)).Returns(dbRef);
            _cachedDbConnector.Setup(x => x.GetFileListCachedAsync(dbRef, CancellationToken.None))
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
            _cachedDbConnector.Setup(x => x.GetFileListCachedAsync(dbRef, CancellationToken.None))
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
            _cachedDbConnector.Setup(x => x.GetFileListCachedAsync(dbRef, CancellationToken.None))
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
        public void ClearTableListCache_DatabaseExists_ClearsOnlyTableList_ReturnsOk()
        {
            DataBaseRef dbRef = DataBaseRef.Create("testdb");
            _cachedDbConnector.Setup(connector => connector.GetDataBaseReference("testdb")).Returns(dbRef);

            ActionResult result = _controller.ClearTableListCache("testdb");

            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            using (Assert.EnterMultipleScope())
            {
                Assert.That(((OkObjectResult)result).StatusCode, Is.EqualTo(200));
                Assert.That(((OkObjectResult)result).Value, Is.EqualTo("Cached table list cleared successfully"));
            }
            _cachedDbConnector.Verify(connector => connector.GetDataBaseReference("testdb"), Times.Once);
            _cachedDbConnector.Verify(connector => connector.ClearTableListCache(dbRef), Times.Once);
            _cachedDbConnector.VerifyNoOtherCalls();
        }

        [Test]
        public void ClearTableListCache_DatabaseDoesNotExist_ReturnsNotFound()
        {
            _cachedDbConnector.Setup(connector => connector.GetDataBaseReference("missing")).Returns((DataBaseRef?)null);

            ActionResult result = _controller.ClearTableListCache("missing");

            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
            using (Assert.EnterMultipleScope())
            {
                Assert.That(((NotFoundObjectResult)result).StatusCode, Is.EqualTo(404));
                Assert.That(((NotFoundObjectResult)result).Value, Is.EqualTo("Database not found"));
            }
            _cachedDbConnector.Verify(connector => connector.GetDataBaseReference("missing"), Times.Once);
            _cachedDbConnector.VerifyNoOtherCalls();
        }

        [Test]
        public void ClearTableListCache_ExceptionThrown_ReturnsStatusCode500()
        {
            DataBaseRef dbRef = DataBaseRef.Create("testdb");
            _cachedDbConnector.Setup(connector => connector.GetDataBaseReference("testdb")).Returns(dbRef);
            _cachedDbConnector.Setup(connector => connector.ClearTableListCache(dbRef))
                .Throws(new InvalidOperationException("Internal failure"));

            ActionResult result = _controller.ClearTableListCache("testdb");

            Assert.That(result, Is.InstanceOf<ObjectResult>());
            using (Assert.EnterMultipleScope())
            {
                Assert.That(((ObjectResult)result).StatusCode, Is.EqualTo(500));
                Assert.That(((ObjectResult)result).Value, Is.EqualTo("Error clearing cached table list"));
            }
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