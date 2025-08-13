using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using PxApi.Caching;
using PxApi.Configuration;
using PxApi.Controllers;
using PxApi.Models;

namespace PxApi.UnitTests.ControllerTests
{
    [TestFixture]
    public class CacheControllerTests
    {
        private Mock<ICachedDataBaseConnector> _cachedDbConnector = null!;
        private Mock<ILogger<CacheController>> _mockLogger = null!;
        private CacheController _controller = null!;

        [SetUp]
        public void SetUp()
        {
            _cachedDbConnector = new Mock<ICachedDataBaseConnector>();
            _mockLogger = new Mock<ILogger<CacheController>>();
            _controller = new CacheController(_cachedDbConnector.Object, _mockLogger.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };

            Dictionary<string, string?> inMemorySettings = new()
            {
                {"RootUrl", "https://testurl.fi"},
                {"DataBases:0:Type", "Mounted"},
                {"DataBases:0:Id", "testdb"},
                {"DataBases:0:CacheConfig:TableList:SlidingExpirationSeconds", "900"},
                {"DataBases:0:CacheConfig:TableList:AbsoluteExpirationSeconds", "900"},
                {"DataBases:0:CacheConfig:Meta:SlidingExpirationSeconds", "900"},
                {"DataBases:0:CacheConfig:Meta:AbsoluteExpirationSeconds", "900"},
                {"DataBases:0:CacheConfig:Data:SlidingExpirationSeconds", "600"},
                {"DataBases:0:CacheConfig:Data:AbsoluteExpirationSeconds", "600"},
                {"DataBases:0:CacheConfig:Modifiedtime:SlidingExpirationSeconds", "60"},
                {"DataBases:0:CacheConfig:Modifiedtime:AbsoluteExpirationSeconds", "60"},
                {"DataBases:0:CacheConfig:MaxCacheSize", "1073741824"},
                {"DataBases:0:Custom:RootPath", "datasource/root/"}
            };

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            AppSettings.Load(configuration);
        }

        [Test]
        public void ClearFileListCache_CallsClearFileListCacheMethod_ReturnsOk()
        {
            // Arrange
            DataBaseRef dbRef = DataBaseRef.Create("testdb");
            _cachedDbConnector.Setup(x => x.GetDataBaseReference("testdb")).Returns(dbRef);
            _cachedDbConnector.Setup(x => x.ClearFileListCache(dbRef));

            // Act
            ActionResult result = _controller.ClearFileListCache("testdb");

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            _cachedDbConnector.Verify(x => x.ClearFileListCache(dbRef), Times.Once);
        }

        [Test]
        public void ClearMetadataCache_DatabaseExists_CallsClearMetadataCacheMethod_ReturnsOk()
        {
            // Arrange
            string database = "testdb";
            DataBaseRef dbRef = DataBaseRef.Create(database);
            _cachedDbConnector.Setup(x => x.GetDataBaseReference(database)).Returns(dbRef);
            _cachedDbConnector.Setup(x => x.ClearMetadataCacheAsync(dbRef));

            // Act
            ActionResult result = _controller.ClearMetadataCache(database);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            _cachedDbConnector.Verify(x => x.ClearMetadataCacheAsync(dbRef), Times.Once);
        }

        [Test]
        public void ClearMetadataCache_DatabaseDoesNotExist_ReturnsNotFound()
        {
            // Arrange
            string database = "nonexistentdb";
            _cachedDbConnector.Setup(x => x.GetDataBaseReference(database)).Returns((DataBaseRef?)null);

            // Act
            ActionResult result = _controller.ClearMetadataCache(database);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
            _cachedDbConnector.Verify(x => x.ClearMetadataCacheAsync(It.IsAny<DataBaseRef>()), Times.Never);
        }

        [Test]
        public void ClearDataCache_DatabaseExists_CallsClearDataCacheMethod_ReturnsOk()
        {
            // Arrange
            string database = "testdb";
            DataBaseRef dbRef = DataBaseRef.Create(database);
            _cachedDbConnector.Setup(x => x.GetDataBaseReference(database)).Returns(dbRef);
            _cachedDbConnector.Setup(x => x.ClearDataCacheAsync(dbRef));

            // Act
            ActionResult result = _controller.ClearDataCache(database);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            _cachedDbConnector.Verify(x => x.ClearDataCacheAsync(dbRef), Times.Once);
        }

        [Test]
        public void ClearDataCache_DatabaseDoesNotExist_ReturnsNotFound()
        {
            // Arrange
            string database = "nonexistentdb";
            _cachedDbConnector.Setup(x => x.GetDataBaseReference(database)).Returns((DataBaseRef?)null);

            // Act
            ActionResult result = _controller.ClearDataCache(database);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
            _cachedDbConnector.Verify(x => x.ClearDataCacheAsync(It.IsAny<DataBaseRef>()), Times.Never);
        }

        [Test]
        public void ClearHierarchyCache_DatabaseExists_CallsClearHierarchyCacheMethod_ReturnsOk()
        {
            // Arrange
            string database = "testdb";
            DataBaseRef dbRef = DataBaseRef.Create(database);
            _cachedDbConnector.Setup(x => x.GetDataBaseReference(database)).Returns(dbRef);
            _cachedDbConnector.Setup(x => x.ClearHierarchyCache(dbRef));

            // Act
            ActionResult result = _controller.ClearHierarchyCache(database);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            _cachedDbConnector.Verify(x => x.ClearHierarchyCache(dbRef), Times.Once);
        }

        [Test]
        public void ClearHierarchyCache_DatabaseDoesNotExist_ReturnsNotFound()
        {
            // Arrange
            string database = "nonexistentdb";
            _cachedDbConnector.Setup(x => x.GetDataBaseReference(database)).Returns((DataBaseRef?)null);

            // Act
            ActionResult result = _controller.ClearHierarchyCache(database);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
            _cachedDbConnector.Verify(x => x.ClearHierarchyCache(It.IsAny<DataBaseRef>()), Times.Never);
        }

        [Test]
        public void ClearAllCache_DatabaseExists_CallsClearAllCacheMethod_ReturnsOk()
        {
            // Arrange
            string database = "testdb";
            DataBaseRef dbRef = DataBaseRef.Create(database);
            _cachedDbConnector.Setup(x => x.GetDataBaseReference(database)).Returns(dbRef);
            _cachedDbConnector.Setup(x => x.ClearAllCache(dbRef));

            // Act
            ActionResult result = _controller.ClearAllCache(database);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            _cachedDbConnector.Verify(x => x.ClearAllCache(dbRef), Times.Once);
        }

        [Test]
        public void ClearAllCache_DatabaseDoesNotExist_ReturnsNotFound()
        {
            // Arrange
            string database = "nonexistentdb";
            _cachedDbConnector.Setup(x => x.GetDataBaseReference(database)).Returns((DataBaseRef?)null);

            // Act
            ActionResult result = _controller.ClearAllCache(database);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
            _cachedDbConnector.Verify(x => x.ClearAllCache(It.IsAny<DataBaseRef>()), Times.Never);
        }

        [Test]
        public void ClearFileListCache_ExceptionThrown_ReturnsStatusCode500()
        {
            // Arrange
            DataBaseRef dbRef = DataBaseRef.Create("testdb");
            _cachedDbConnector.Setup(x => x.GetDataBaseReference("testdb")).Returns(dbRef);
            _cachedDbConnector.Setup(x => x.ClearFileListCache(dbRef)).Throws(new Exception("Test exception"));

            // Act
            ActionResult result = _controller.ClearFileListCache("testdb");

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            ObjectResult? statusCodeResult = result as ObjectResult;
            Assert.That(statusCodeResult, Is.Not.Null);
            Assert.That(statusCodeResult!.StatusCode, Is.EqualTo(500));
        }

        [Test]
        public async Task ClearAllMetadataCaches_CallsGetAllDataBaseReferencesAndClearMetadataCache_ReturnsOk()
        {
            // Arrange
            DataBaseRef[] dbRefs = {DataBaseRef.Create("testdb1"), DataBaseRef.Create("testdb2")};
            _cachedDbConnector.Setup(x => x.GetAllDataBaseReferences()).Returns(dbRefs);
            _cachedDbConnector.Setup(x => x.ClearMetadataCacheAsync(It.IsAny<DataBaseRef>())).Returns(Task.CompletedTask);

            // Act
            ActionResult result = await _controller.ClearAllMetadataCaches();

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            _cachedDbConnector.Verify(x => x.GetAllDataBaseReferences(), Times.Once);
            _cachedDbConnector.Verify(x => x.ClearMetadataCacheAsync(dbRefs[0]), Times.Once);
            _cachedDbConnector.Verify(x => x.ClearMetadataCacheAsync(dbRefs[1]), Times.Once);
        }

        [Test]
        public async Task ClearAllDataCaches_CallsGetAllDataBaseReferencesAndClearDataCache_ReturnsOk()
        {
            // Arrange
            DataBaseRef[] dbRefs = {DataBaseRef.Create("testdb1"), DataBaseRef.Create("testdb2")};
            _cachedDbConnector.Setup(x => x.GetAllDataBaseReferences()).Returns(dbRefs);
            _cachedDbConnector.Setup(x => x.ClearDataCacheAsync(It.IsAny<DataBaseRef>())).Returns(Task.CompletedTask);

            // Act
            ActionResult result = await _controller.ClearAllDataCaches();

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            _cachedDbConnector.Verify(x => x.GetAllDataBaseReferences(), Times.Once);
            _cachedDbConnector.Verify(x => x.ClearDataCacheAsync(dbRefs[0]), Times.Once);
            _cachedDbConnector.Verify(x => x.ClearDataCacheAsync(dbRefs[1]), Times.Once);
        }

        [Test]
        public void ClearAllHierarchyCaches_CallsGetAllDataBaseReferencesAndClearHierarchyCache_ReturnsOk()
        {
            // Arrange
            DataBaseRef[] dbRefs = {DataBaseRef.Create("testdb1"), DataBaseRef.Create("testdb2")};
            _cachedDbConnector.Setup(x => x.GetAllDataBaseReferences()).Returns(dbRefs);
            _cachedDbConnector.Setup(x => x.ClearHierarchyCache(It.IsAny<DataBaseRef>()));

            // Act
            ActionResult result = _controller.ClearAllHierarchyCaches();

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            _cachedDbConnector.Verify(x => x.GetAllDataBaseReferences(), Times.Once);
            _cachedDbConnector.Verify(x => x.ClearHierarchyCache(dbRefs[0]), Times.Once);
            _cachedDbConnector.Verify(x => x.ClearHierarchyCache(dbRefs[1]), Times.Once);
        }

        [Test]
        public async Task ClearAllCaches_CallsClearAllCachesAsync_ReturnsOk()
        {
            // Arrange
            _cachedDbConnector.Setup(x => x.ClearAllCachesAsync()).Returns(Task.CompletedTask);

            // Act
            ActionResult result = await _controller.ClearAllCaches();

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            _cachedDbConnector.Verify(x => x.ClearAllCachesAsync(), Times.Once);
        }

        [Test]
        public async Task ClearAllMetadataCaches_ExceptionThrown_ReturnsStatusCode500()
        {
            // Arrange
            _cachedDbConnector.Setup(x => x.GetAllDataBaseReferences()).Throws(new Exception("Test exception"));

            // Act
            ActionResult result = await _controller.ClearAllMetadataCaches();

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            ObjectResult? statusCodeResult = result as ObjectResult;
            Assert.That(statusCodeResult, Is.Not.Null);
            Assert.That(statusCodeResult!.StatusCode, Is.EqualTo(500));
        }

        [Test]
        public async Task ClearAllDataCaches_ExceptionThrown_ReturnsStatusCode500()
        {
            // Arrange
            _cachedDbConnector.Setup(x => x.GetAllDataBaseReferences()).Throws(new Exception("Test exception"));

            // Act
            ActionResult result = await _controller.ClearAllDataCaches();

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            ObjectResult? statusCodeResult = result as ObjectResult;
            Assert.That(statusCodeResult, Is.Not.Null);
            Assert.That(statusCodeResult!.StatusCode, Is.EqualTo(500));
        }

        [Test]
        public void ClearAllHierarchyCaches_ExceptionThrown_ReturnsStatusCode500()
        {
            // Arrange
            _cachedDbConnector.Setup(x => x.GetAllDataBaseReferences()).Throws(new Exception("Test exception"));

            // Act
            ActionResult result = _controller.ClearAllHierarchyCaches();

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            ObjectResult? statusCodeResult = result as ObjectResult;
            Assert.That(statusCodeResult, Is.Not.Null);
            Assert.That(statusCodeResult!.StatusCode, Is.EqualTo(500));
        }

        [Test]
        public async Task ClearAllCaches_ExceptionThrown_ReturnsStatusCode500()
        {
            // Arrange
            _cachedDbConnector.Setup(x => x.ClearAllCachesAsync()).Throws(new Exception("Test exception"));

            // Act
            ActionResult result = await _controller.ClearAllCaches();

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            ObjectResult? statusCodeResult = result as ObjectResult;
            Assert.That(statusCodeResult, Is.Not.Null);
            Assert.That(statusCodeResult!.StatusCode, Is.EqualTo(500));
        }

        [Test]
        public async Task ClearLastUpdatedCache_DatabaseExists_CallsClearLastUpdatedCacheMethod_ReturnsOk()
        {
            // Arrange
            string database = "testdb";
            DataBaseRef dbRef = DataBaseRef.Create(database);
            _cachedDbConnector.Setup(x => x.GetDataBaseReference(database)).Returns(dbRef);
            _cachedDbConnector.Setup(x => x.ClearLastUpdatedCacheAsync(dbRef)).Returns(Task.CompletedTask);

            // Act
            ActionResult result = await _controller.ClearLastUpdatedCache(database);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            _cachedDbConnector.Verify(x => x.ClearLastUpdatedCacheAsync(dbRef), Times.Once);
        }

        [Test]
        public async Task ClearLastUpdatedCache_DatabaseDoesNotExist_ReturnsNotFound()
        {
            // Arrange
            string database = "nonexistentdb";
            _cachedDbConnector.Setup(x => x.GetDataBaseReference(database)).Returns((DataBaseRef?)null);

            // Act
            ActionResult result = await _controller.ClearLastUpdatedCache(database);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
            _cachedDbConnector.Verify(x => x.ClearLastUpdatedCacheAsync(It.IsAny<DataBaseRef>()), Times.Never);
        }

        [Test]
        public async Task ClearLastUpdatedCache_ExceptionThrown_ReturnsStatusCode500()
        {
            // Arrange
            string database = "testdb";
            DataBaseRef dbRef = DataBaseRef.Create(database);
            _cachedDbConnector.Setup(x => x.GetDataBaseReference(database)).Returns(dbRef);
            _cachedDbConnector.Setup(x => x.ClearLastUpdatedCacheAsync(dbRef)).Throws(new Exception("Test exception"));

            // Act
            ActionResult result = await _controller.ClearLastUpdatedCache(database);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            ObjectResult? statusCodeResult = result as ObjectResult;
            Assert.That(statusCodeResult, Is.Not.Null);
            Assert.That(statusCodeResult!.StatusCode, Is.EqualTo(500));
        }

        [Test]
        public async Task ClearAllLastUpdatedCaches_CallsGetAllDataBaseReferencesAndClearLastUpdatedCache_ReturnsOk()
        {
            // Arrange
            DataBaseRef[] dbRefs = {DataBaseRef.Create("testdb1"), DataBaseRef.Create("testdb2")};
            _cachedDbConnector.Setup(x => x.GetAllDataBaseReferences()).Returns(dbRefs);
            _cachedDbConnector.Setup(x => x.ClearLastUpdatedCacheAsync(It.IsAny<DataBaseRef>())).Returns(Task.CompletedTask);

            // Act
            ActionResult result = await _controller.ClearAllLastUpdatedCaches();

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            _cachedDbConnector.Verify(x => x.GetAllDataBaseReferences(), Times.Once);
            _cachedDbConnector.Verify(x => x.ClearLastUpdatedCacheAsync(dbRefs[0]), Times.Once);
            _cachedDbConnector.Verify(x => x.ClearLastUpdatedCacheAsync(dbRefs[1]), Times.Once);
        }

        [Test]
        public async Task ClearAllLastUpdatedCaches_ExceptionThrown_ReturnsStatusCode500()
        {
            // Arrange
            _cachedDbConnector.Setup(x => x.GetAllDataBaseReferences()).Throws(new Exception("Test exception"));

            // Act
            ActionResult result = await _controller.ClearAllLastUpdatedCaches();

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            ObjectResult? statusCodeResult = result as ObjectResult;
            Assert.That(statusCodeResult, Is.Not.Null);
            Assert.That(statusCodeResult!.StatusCode, Is.EqualTo(500));
        }
    }
}