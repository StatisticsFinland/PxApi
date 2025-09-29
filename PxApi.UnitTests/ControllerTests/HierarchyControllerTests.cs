/*using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using PxApi.Caching;
using PxApi.Configuration;
using PxApi.Controllers;
using PxApi.DataSources;
using PxApi.Models;

namespace PxApi.UnitTests.ControllerTests
{
    [TestFixture]
    public class HierarchyControllerTests
    {
        private Mock<ICachedDataSource> _cachedDbConnector;
        private Mock<ILogger<GroupingsController>> _mockLogger;
        private GroupingsController _controller;

        [SetUp]
        public void SetUp()
        {
            _cachedDbConnector = new Mock<ICachedDataSource>();
            _mockLogger = new Mock<ILogger<GroupingsController>>();
            _controller = new GroupingsController(_cachedDbConnector.Object, _mockLogger.Object)
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
                    {"DataBases:0:Id", "PxApiUnitTestsDb"},
                    {"DataBases:0:CacheConfig:TableList:SlidingExpirationSeconds", "900"},
                    {"DataBases:0:CacheConfig:TableList:AbsoluteExpirationSeconds", "900"},
                    {"DataBases:0:CacheConfig:Meta:SlidingExpirationSeconds", "900"}, // 15 minutes
                    {"DataBases:0:CacheConfig:Meta:AbsoluteExpirationSeconds", "900"}, // 15 minutes
                    {"DataBases:0:CacheConfig:Data:SlidingExpirationSeconds", "600"}, // 10 minutes
                    {"DataBases:0:CacheConfig:Data:AbsoluteExpirationSeconds", "600"}, // 10 minutes
                    {"DataBases:0:CacheConfig:Modifiedtime:SlidingExpirationSeconds", "60"},
                    {"DataBases:0:CacheConfig:Modifiedtime:AbsoluteExpirationSeconds", "60"},
                    {"DataBases:0:CacheConfig:GroupingsConfig:SlidingExpirationSeconds", "1800"}, // 30 minutes
                    {"DataBases:0:CacheConfig:GroupingsConfig:AbsoluteExpirationSeconds", "1800"}, // 30 minutes
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

        #region GetHierarchy Tests

        [Test]
        public void GetHierarchy_DatabaseNotFound_ReturnsNotFound()
        {
            // Arrange
            string databaseId = "nonexistentdb";
            _cachedDbConnector.Setup(x => x.GetDataBaseReference(databaseId)).Returns((DataBaseRef?)null);

            // Act
            ActionResult<Dictionary<Groupings, List<PxFileRef>>> result = _controller.GetGroupings(databaseId);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<NotFoundObjectResult>());
            NotFoundObjectResult? notFoundResult = result.Result as NotFoundObjectResult;
            Assert.That(notFoundResult, Is.Not.Null);
            Assert.That(notFoundResult!.Value, Is.EqualTo("Database not found"));
        }

        [Test]
        public void GetHierarchy_HierarchyNotFound_ReturnsNotFound()
        {
            // Arrange
            string databaseId = "testdb";
            DataBaseRef dbRef = DataBaseRef.Create(databaseId);
            _cachedDbConnector.Setup(x => x.GetDataBaseReference(databaseId)).Returns(dbRef);
            _cachedDbConnector
                .Setup(x => x.TryGetDataBaseGroupings(dbRef, out It.Ref<Dictionary<string, List<string>>?>.IsAny))
                .Returns(false);

            // Act
            ActionResult<Dictionary<Groupings, List<PxFileRef>>> result = _controller.GetGroupings(databaseId);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<NotFoundObjectResult>());
            NotFoundObjectResult? notFoundResult = result.Result as NotFoundObjectResult;
            Assert.That(notFoundResult, Is.Not.Null);
            Assert.That(notFoundResult!.Value, Is.EqualTo("Hierarchy not found"));
        }

        [Test]
        public void GetHierarchy_HierarchyFound_ReturnsOkWithHierarchy()
        {
            // Arrange
            string databaseId = "testdb";
            DataBaseRef dbRef = DataBaseRef.Create(databaseId);
            Dictionary<string, List<string>> expectedHierarchy = new()
            {
                { "group1", new List<string> { "file1", "file2" } },
                { "group2", new List<string> { "file3", "file4" } }
            };

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(databaseId)).Returns(dbRef);
            _cachedDbConnector
                .Setup(x => x.TryGetDataBaseGroupings(dbRef, out It.Ref<Dictionary<string, List<string>>?>.IsAny))
                .Callback(new TryGetDataBaseHierarchyCallback((DataBaseRef dbr, out Dictionary<string, List<string>>? h) =>
                {
                    h = expectedHierarchy;
                }))
                .Returns(true);

            // Act
            ActionResult<Dictionary<Groupings, List<PxFileRef>>> result = _controller.GetGroupings(databaseId);

            // Assert
            Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
            OkObjectResult? okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            Assert.That(okResult!.Value, Is.SameAs(expectedHierarchy));
        }

        #endregion

        #region UpdateHierarchy Tests

        [Test]
        public void UpdateHierarchy_DatabaseNotFound_ReturnsNotFound()
        {
            // Arrange
            string databaseId = "nonexistentdb";
            Dictionary<string, List<string>> hierarchyToUpdate = new()
            {
                { "group1", new List<string> { "file1", "file2" } }
            };

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(databaseId)).Returns((DataBaseRef?)null);

            // Act
            ActionResult result = _controller.UpdateGroupings(databaseId, hierarchyToUpdate);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
            NotFoundObjectResult? notFoundResult = result as NotFoundObjectResult;
            Assert.That(notFoundResult, Is.Not.Null);
            Assert.That(notFoundResult!.Value, Is.EqualTo("Database not found"));
        }

        [Test]
        public void UpdateHierarchy_ValidDatabase_CallsSetDataBaseHierarchy_ReturnsOk()
        {
            // Arrange
            string databaseId = "testdb";
            DataBaseRef dbRef = DataBaseRef.Create(databaseId);
            Dictionary<string, List<string>> hierarchyToUpdate = new()
            {
                { "group1", new List<string> { "file1", "file2" } },
                { "group2", new List<string> { "file3", "file4" } }
            };

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(databaseId)).Returns(dbRef);

            // Act
            ActionResult result = _controller.UpdateGroupings(databaseId, hierarchyToUpdate);

            // Assert
            _cachedDbConnector.Verify(c => c.SetDataBaseGroupings(dbRef, hierarchyToUpdate), Times.Once);
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public void UpdateHierarchy_InvalidOperation_ReturnsBadRequest()
        {
            // Arrange
            string databaseId = "testdb";
            DataBaseRef dbRef = DataBaseRef.Create(databaseId);
            Dictionary<string, List<string>> hierarchyToUpdate = new()
            {
                { "group1", new List<string> { "file1", "file2" } }
            };

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(databaseId)).Returns(dbRef);
            _cachedDbConnector
                .Setup(x => x.SetDataBaseGroupings(dbRef, hierarchyToUpdate))
                .Throws(new InvalidOperationException("Hierarchy not configured"));

            // Act
            ActionResult result = _controller.UpdateGroupings(databaseId, hierarchyToUpdate);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
            BadRequestObjectResult? badRequestResult = result as BadRequestObjectResult;
            Assert.That(badRequestResult, Is.Not.Null);
            Assert.That(badRequestResult!.Value!.ToString(), Does.Contain("Invalid operation"));
        }

        [Test]
        public void UpdateHierarchy_UnexpectedException_Returns500Error()
        {
            // Arrange
            string databaseId = "testdb";
            DataBaseRef dbRef = DataBaseRef.Create(databaseId);
            Dictionary<string, List<string>> hierarchyToUpdate = new()
            {
                { "group1", new List<string> { "file1", "file2" } }
            };

            _cachedDbConnector.Setup(x => x.GetDataBaseReference(databaseId)).Returns(dbRef);
            _cachedDbConnector
                .Setup(x => x.SetDataBaseGroupings(dbRef, hierarchyToUpdate))
                .Throws(new Exception("Unexpected error"));

            // Act
            ActionResult result = _controller.UpdateGroupings(databaseId, hierarchyToUpdate);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            ObjectResult? statusCodeResult = result as ObjectResult;
            Assert.That(statusCodeResult, Is.Not.Null);
            Assert.That(statusCodeResult!.StatusCode, Is.EqualTo(500));
            Assert.That(statusCodeResult.Value, Is.EqualTo("Error updating hierarchy"));
        }

        #endregion

        // Helper delegate for testing TryGetDataBaseGroupings
        private delegate void TryGetDataBaseHierarchyCallback(DataBaseRef dbRef, out Dictionary<string, List<string>>? hierarchy);
    }
}*/