using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using PxApi.Controllers;
using PxApi.DataSources;
using PxApi.Models;
using PxApi.Services;
using PxApi.UnitTests.Utils;

namespace PxApi.UnitTests.ControllerTests
{
    [TestFixture]
    public class HealthControllerTests
    {
        private Mock<ILogger<HealthController>> _mockLogger = null!;
        private Mock<ISearchService> _mockSearchService = null!;
        private ServiceCollection _services = null!;

        [SetUp]
        public void SetUp()
        {
            _mockLogger = new Mock<ILogger<HealthController>>();
            _mockSearchService = new Mock<ISearchService>();
            _mockSearchService.Setup(s => s.CheckHealthAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

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
            mockConnector1.Setup(c => c.CheckConnectionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            Mock<IDataBaseConnector> mockConnector2 = new();
            mockConnector2.Setup(c => c.CheckConnectionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _services.AddKeyedScoped<IDataBaseConnector>("db1", (_, _) => mockConnector1.Object);
            _services.AddKeyedScoped<IDataBaseConnector>("db2", (_, _) => mockConnector2.Object);
            _services.AddScoped<ISearchService>(_ => _mockSearchService.Object);
            ServiceProvider serviceProvider = _services.BuildServiceProvider();

            HealthController controller = new(serviceProvider, _mockLogger.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            // Act
            IActionResult result = await controller.GetHealthAsync(CancellationToken.None);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            OkObjectResult okResult = (OkObjectResult)result;
            HealthResponse response = (HealthResponse)okResult.Value!;
            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.Status, Is.EqualTo(HealthStatus.Healthy));
                Assert.That(response.Databases, Has.Count.EqualTo(2));
                Assert.That(response.Databases[0].Status, Is.EqualTo(HealthStatus.Healthy));
                Assert.That(response.Databases[1].Status, Is.EqualTo(HealthStatus.Healthy));
                Assert.That(response.Search, Is.Not.Null);
                Assert.That(response.Search!.Status, Is.EqualTo(HealthStatus.Healthy));
            }
        }

        [Test]
        public async Task GetHealth_OneDatabaseUnhealthy_Returns503WithUnhealthyStatus()
        {
            // Arrange
            Mock<IDataBaseConnector> mockConnector1 = new();
            mockConnector1.Setup(c => c.CheckConnectionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            Mock<IDataBaseConnector> mockConnector2 = new();
            mockConnector2.Setup(c => c.CheckConnectionAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Connection failed"));

            _services.AddKeyedScoped<IDataBaseConnector>("db1", (_, _) => mockConnector1.Object);
            _services.AddKeyedScoped<IDataBaseConnector>("db2", (_, _) => mockConnector2.Object);
            _services.AddScoped<ISearchService>(_ => _mockSearchService.Object);
            ServiceProvider serviceProvider = _services.BuildServiceProvider();

            HealthController controller = new(serviceProvider, _mockLogger.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            // Act
            IActionResult result = await controller.GetHealthAsync(CancellationToken.None);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            ObjectResult objectResult = (ObjectResult)result;
            HealthResponse response = (HealthResponse)objectResult.Value!;
            using (Assert.EnterMultipleScope())
            {
                Assert.That(objectResult.StatusCode, Is.EqualTo(503));
                Assert.That(response.Status, Is.EqualTo(HealthStatus.Unhealthy));
                Assert.That(response.Databases[0].Status, Is.EqualTo(HealthStatus.Healthy));
                Assert.That(response.Databases[1].Status, Is.EqualTo(HealthStatus.Unhealthy));
            }
        }

        [Test]
        public async Task GetHealth_NoDatabases_ReturnsOkWithHealthyStatus()
        {
            // Arrange - config with no databases
            Dictionary<string, string?> configData = TestConfigFactory.Base();
            TestConfigFactory.BuildAndLoad(configData);

            _services.AddScoped<ISearchService>(_ => _mockSearchService.Object);
            ServiceProvider serviceProvider = _services.BuildServiceProvider();
            HealthController controller = new(serviceProvider, _mockLogger.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            // Act
            IActionResult result = await controller.GetHealthAsync(CancellationToken.None);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            OkObjectResult okResult = (OkObjectResult)result;
            HealthResponse response = (HealthResponse)okResult.Value!;
            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.Status, Is.EqualTo(HealthStatus.Healthy));
                Assert.That(response.Databases, Is.Empty);
                Assert.That(response.Search, Is.Not.Null);
                Assert.That(response.Search!.Status, Is.EqualTo(HealthStatus.Healthy));
            }
        }

        [Test]
        public void GetHealth_CancellationRequested_ThrowsOperationCanceledException()
        {
            // Arrange
            using CancellationTokenSource cts = new();
            cts.Cancel();

            Mock<IDataBaseConnector> mockConnector1 = new();
            mockConnector1.Setup(c => c.CheckConnectionAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            _services.AddKeyedScoped<IDataBaseConnector>("db1", (_, _) => mockConnector1.Object);
            _services.AddScoped<ISearchService>(_ => _mockSearchService.Object);
            ServiceProvider serviceProvider = _services.BuildServiceProvider();

            HealthController controller = new(serviceProvider, _mockLogger.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            // Act & Assert
            Assert.That(async () => await controller.GetHealthAsync(cts.Token),
                Throws.InstanceOf<OperationCanceledException>());
        }

        [Test]
        public async Task GetHealth_SearchUnhealthy_Returns503WithSearchStatus()
        {
            // Arrange
            Mock<IDataBaseConnector> mockConnector1 = new();
            mockConnector1.Setup(c => c.CheckConnectionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            Mock<ISearchService> unhealthySearch = new();
            unhealthySearch.Setup(s => s.CheckHealthAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Search backend unavailable"));

            _services.AddKeyedScoped<IDataBaseConnector>("db1", (_, _) => mockConnector1.Object);
            _services.AddKeyedScoped<IDataBaseConnector>("db2", (_, _) => mockConnector1.Object);
            _services.AddScoped<ISearchService>(_ => unhealthySearch.Object);
            ServiceProvider serviceProvider = _services.BuildServiceProvider();

            HealthController controller = new(serviceProvider, _mockLogger.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            // Act
            IActionResult result = await controller.GetHealthAsync(CancellationToken.None);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            ObjectResult objectResult = (ObjectResult)result;
            HealthResponse response = (HealthResponse)objectResult.Value!;
            using (Assert.EnterMultipleScope())
            {
                Assert.That(objectResult.StatusCode, Is.EqualTo(503));
                Assert.That(response.Status, Is.EqualTo(HealthStatus.Unhealthy));
                Assert.That(response.Databases[0].Status, Is.EqualTo(HealthStatus.Healthy));
                Assert.That(response.Search, Is.Not.Null);
                Assert.That(response.Search!.Status, Is.EqualTo(HealthStatus.Unhealthy));
            }
        }

        [Test]
        public async Task GetHealth_SearchDisabled_ReturnsNoSearchStatus()
        {
            // Arrange - config with search disabled
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                new Dictionary<string, string?> { ["FeatureManagement:SearchController"] = "false" },
                TestConfigFactory.MountedDb(0, "db1", "datasource/root1/")
            );
            TestConfigFactory.BuildAndLoad(configData);

            Mock<IDataBaseConnector> mockConnector1 = new();
            mockConnector1.Setup(c => c.CheckConnectionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _services.AddKeyedScoped<IDataBaseConnector>("db1", (_, _) => mockConnector1.Object);
            ServiceProvider serviceProvider = _services.BuildServiceProvider();

            HealthController controller = new(serviceProvider, _mockLogger.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            // Act
            IActionResult result = await controller.GetHealthAsync(CancellationToken.None);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            OkObjectResult okResult = (OkObjectResult)result;
            HealthResponse response = (HealthResponse)okResult.Value!;
            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.Status, Is.EqualTo(HealthStatus.Healthy));
                Assert.That(response.Search, Is.Null);
            }
        }
    }
}
