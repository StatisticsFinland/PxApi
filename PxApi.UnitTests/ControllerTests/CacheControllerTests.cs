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

            Dictionary<string, string?> inMemorySettings = new()
            {
                {"RootUrl", "https://testurl.fi"},
                {"DataBases:0:Type", "Mounted"},
                {"DataBases:0:Id", "testdb"},
                {"DataBases:0:CacheConfig:TableList:SlidingExpirationSeconds", "900"},
                {"DataBases:0:CacheConfig:TableList:AbsoluteExpirationSeconds", "900"},
                {"DataBases:0:CacheConfig:Meta:SlidingExpirationSeconds", "900"},
                {"DataBases:0:CacheConfig:Meta:AbsoluteExpirationSeconds", "900"},
                {"DataBases:0:CacheConfig:Groupings:SlidingExpirationSeconds", "900"},
                {"DataBases:0:CacheConfig:Groupings:AbsoluteExpirationSeconds", "900"},
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
        public async Task ClearTableCache_DatabaseExists_CallsClearTableCacheMethod_ReturnsOk()
        {
            // Arrange
            string database = "testdb";
            string id = "table1";
            DataBaseRef dbRef = DataBaseRef.Create(database);
            PxFileRef fileRef = PxFileRef.CreateFromId(id, dbRef);
            
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
            string database = "nonexistentdb";
            string id = "table1";
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
            string database = "testdb";
            string id = "nonexistentfile";
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
            string database = "testdb";
            string id = "table1";
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
            string database = "testdb";
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
            string database = "nonexistentdb";
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