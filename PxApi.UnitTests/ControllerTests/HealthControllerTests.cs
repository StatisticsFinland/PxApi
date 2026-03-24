using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using PxApi.Controllers;
using PxApi.DataSources;
using PxApi.Models;
using PxApi.UnitTests.Utils;

namespace PxApi.UnitTests.ControllerTests
{
    [TestFixture]
    public class HealthControllerTests
    {
        private Mock<ILogger<HealthController>> _mockLogger = null!;
        private ServiceCollection _services = null!;

        [SetUp]
        public void SetUp()
        {
            _mockLogger = new Mock<ILogger<HealthController>>();

            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                TestConfigFactory.MountedDb(0, "db1", "datasource/root1/"),
                TestConfigFactory.MountedDb(1, "db2", "datasource/root2/")
            );
            TestConfigFactory.BuildAndLoad(configData);

            _services = new ServiceCollection();
        }

        [Test]
        public async Task GetHealth_AllDatabasesHealthy_ReturnsOkWithHealthyStatus()
        {
            // Arrange
            Mock<IDataBaseConnector> mockConnector1 = new();
            mockConnector1.Setup(c => c.GetAllFilesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            Mock<IDataBaseConnector> mockConnector2 = new();
            mockConnector2.Setup(c => c.GetAllFilesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            _services.AddKeyedScoped("db1", (_, _) => mockConnector1.Object);
            _services.AddKeyedScoped("db2", (_, _) => mockConnector2.Object);
            ServiceProvider serviceProvider = _services.BuildServiceProvider();

            HealthController controller = new(serviceProvider, _mockLogger.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            // Act
            IActionResult result = await controller.GetHealth(CancellationToken.None);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            OkObjectResult okResult = (OkObjectResult)result;
            HealthResponse response = (HealthResponse)okResult.Value!;
            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.Status, Is.EqualTo("healthy"));
                Assert.That(response.Databases, Has.Count.EqualTo(2));
                Assert.That(response.Databases[0].Status, Is.EqualTo("healthy"));
                Assert.That(response.Databases[1].Status, Is.EqualTo("healthy"));
            }
        }

        [Test]
        public async Task GetHealth_OneDatabaseUnhealthy_Returns503WithUnhealthyStatus()
        {
            // Arrange
            Mock<IDataBaseConnector> mockConnector1 = new();
            mockConnector1.Setup(c => c.GetAllFilesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            Mock<IDataBaseConnector> mockConnector2 = new();
            mockConnector2.Setup(c => c.GetAllFilesAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Connection failed"));

            _services.AddKeyedScoped("db1", (_, _) => mockConnector1.Object);
            _services.AddKeyedScoped("db2", (_, _) => mockConnector2.Object);
            ServiceProvider serviceProvider = _services.BuildServiceProvider();

            HealthController controller = new(serviceProvider, _mockLogger.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            // Act
            IActionResult result = await controller.GetHealth(CancellationToken.None);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            ObjectResult objectResult = (ObjectResult)result;
            HealthResponse response = (HealthResponse)objectResult.Value!;
            using (Assert.EnterMultipleScope())
            {
                Assert.That(objectResult.StatusCode, Is.EqualTo(503));
                Assert.That(response.Status, Is.EqualTo("unhealthy"));
                Assert.That(response.Databases[0].Status, Is.EqualTo("healthy"));
                Assert.That(response.Databases[1].Status, Is.EqualTo("unhealthy"));
            }
        }

        [Test]
        public async Task GetHealth_NoDatabases_ReturnsOkWithHealthyStatus()
        {
            // Arrange - config with no databases
            Dictionary<string, string?> configData = TestConfigFactory.Base();
            TestConfigFactory.BuildAndLoad(configData);

            ServiceProvider serviceProvider = _services.BuildServiceProvider();
            HealthController controller = new(serviceProvider, _mockLogger.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            // Act
            IActionResult result = await controller.GetHealth(CancellationToken.None);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            OkObjectResult okResult = (OkObjectResult)result;
            HealthResponse response = (HealthResponse)okResult.Value!;
            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.Status, Is.EqualTo("healthy"));
                Assert.That(response.Databases, Is.Empty);
            }
        }
    }
}
