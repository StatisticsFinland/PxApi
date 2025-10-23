using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using PxApi.DataSources;
using PxApi.UnitTests.Utils;
using PxApi.Utilities;

namespace PxApi.UnitTests.UtilitiesTests
{
    [TestFixture]
    public class ServiceCollectionExtensionsTests
    {
        private IServiceCollection _services;
        private Mock<IServiceProvider> _mockServiceProvider;

        [SetUp]
        public void SetUp()
        {
            _services = new ServiceCollection();
            _mockServiceProvider = new Mock<IServiceProvider>();

            // Setup common logger mocks using GetService instead of GetRequiredService
            _mockServiceProvider.Setup(sp => sp.GetService(typeof(ILogger<MountedDataBaseConnector>)))
                .Returns(new Mock<ILogger<MountedDataBaseConnector>>().Object);
            _mockServiceProvider.Setup(sp => sp.GetService(typeof(ILogger<FileShareDataBaseConnector>)))
               .Returns(new Mock<ILogger<FileShareDataBaseConnector>>().Object);
            _mockServiceProvider.Setup(sp => sp.GetService(typeof(ILogger<BlobStorageDataBaseConnector>)))
               .Returns(new Mock<ILogger<BlobStorageDataBaseConnector>>().Object);
        }

        [TearDown]
        public void TearDown()
        {
            // Reset AppSettings after each test to avoid side effects
            Dictionary<string, string?> emptyConfig = TestConfigFactory.Base();
            TestConfigFactory.BuildAndLoad(emptyConfig);
        }

        [Test]
        public void AddDataBaseConnectors_WithValidMountedDatabase_RegistersServices()
        {
            // Arrange
            SetupAppSettingsWithMountedDatabase();

            // Act
            _services.AddDataBaseConnectors();

            // Assert
            ServiceDescriptor? serviceDescriptor = _services.FirstOrDefault(sd =>
                sd.ServiceType == typeof(IDataBaseConnector) &&
                sd.ServiceKey?.ToString() == "TestMountedDb");

            Assert.Multiple(() =>
            {
                Assert.That(serviceDescriptor, Is.Not.Null);
                Assert.That(serviceDescriptor!.Lifetime, Is.EqualTo(ServiceLifetime.Scoped));
                Assert.That(serviceDescriptor.ServiceKey, Is.EqualTo("TestMountedDb"));
            });
        }

        [Test]
        public void AddDataBaseConnectors_WithValidFileShareDatabase_RegistersServices()
        {
            // Arrange
            SetupAppSettingsWithFileShareDatabase();

            // Act
            _services.AddDataBaseConnectors();

            // Assert
            ServiceDescriptor? serviceDescriptor = _services.FirstOrDefault(sd =>
                sd.ServiceType == typeof(IDataBaseConnector) &&
                sd.ServiceKey?.ToString() == "TestFileShareDb");

            Assert.Multiple(() =>
            {
                Assert.That(serviceDescriptor, Is.Not.Null);
                Assert.That(serviceDescriptor!.Lifetime, Is.EqualTo(ServiceLifetime.Scoped));
                Assert.That(serviceDescriptor.ServiceKey, Is.EqualTo("TestFileShareDb"));
            });
        }

        [Test]
        public void AddDataBaseConnectors_WithValidBlobStorageDatabase_RegistersServices()
        {
            // Arrange
            SetupAppSettingsWithBlobStorageDatabase();

            // Act
            _services.AddDataBaseConnectors();

            // Assert
            ServiceDescriptor? serviceDescriptor = _services.FirstOrDefault(sd =>
                sd.ServiceType == typeof(IDataBaseConnector) &&
                sd.ServiceKey?.ToString() == "TestBlobStorageDb");

            Assert.Multiple(() =>
            {
                Assert.That(serviceDescriptor, Is.Not.Null);
                Assert.That(serviceDescriptor!.Lifetime, Is.EqualTo(ServiceLifetime.Scoped));
                Assert.That(serviceDescriptor.ServiceKey, Is.EqualTo("TestBlobStorageDb"));
            });
        }

        [Test]
        public void AddDataBaseConnectors_WithMultipleDatabases_RegistersAllServices()
        {
            // Arrange
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                TestConfigFactory.MountedDb(0, "MountedDb"),
                TestConfigFactory.FileShareDb(1, "FileShareDb"),
                TestConfigFactory.BlobStorageDb(2, "BlobStorageDb")
            );
            TestConfigFactory.BuildAndLoad(configData);

            // Act
            _services.AddDataBaseConnectors();

            // Assert
            List<ServiceDescriptor> connectorServices = [.. _services.Where(sd => sd.ServiceType == typeof(IDataBaseConnector))];

            Assert.Multiple(() =>
            {
                Assert.That(connectorServices, Has.Count.EqualTo(3));
                Assert.That(connectorServices.Select(sd => sd.ServiceKey?.ToString()),
                           Contains.Item("MountedDb"));
                Assert.That(connectorServices.Select(sd => sd.ServiceKey?.ToString()),
                           Contains.Item("FileShareDb"));
                Assert.That(connectorServices.Select(sd => sd.ServiceKey?.ToString()),
                           Contains.Item("BlobStorageDb"));
            });
        }

        [Test]
        public void AddDataBaseConnectors_MountedConnectorFactory_CreatesCorrectConnector()
        {
            // Arrange
            SetupAppSettingsWithMountedDatabase();
            _services.AddDataBaseConnectors();
            ServiceDescriptor serviceDescriptor = _services.First(sd =>
                sd.ServiceType == typeof(IDataBaseConnector) &&
                sd.ServiceKey?.ToString() == "TestMountedDb");

            // Act
            IDataBaseConnector connector = (IDataBaseConnector)serviceDescriptor.KeyedImplementationFactory!(_mockServiceProvider.Object, "TestMountedDb");

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(connector, Is.InstanceOf<MountedDataBaseConnector>());
                Assert.That(connector.DataBase.Id, Is.EqualTo("TestMountedDb"));
            });
        }

        [Test]
        public void AddDataBaseConnectors_FileShareConnectorFactory_RegistersCorrectService()
        {
            // Arrange
            SetupAppSettingsWithFileShareDatabase();

            // Act
            _services.AddDataBaseConnectors();

            // Assert
            ServiceDescriptor? serviceDescriptor = _services.FirstOrDefault(sd =>
                sd.ServiceType == typeof(IDataBaseConnector) &&
                sd.ServiceKey?.ToString() == "TestFileShareDb");

            Assert.Multiple(() =>
            {
                Assert.That(serviceDescriptor, Is.Not.Null);
                Assert.That(serviceDescriptor!.Lifetime, Is.EqualTo(ServiceLifetime.Scoped));
                Assert.That(serviceDescriptor.ServiceKey, Is.EqualTo("TestFileShareDb"));
                Assert.That(serviceDescriptor.KeyedImplementationFactory, Is.Not.Null);
            });
        }

        [Test]
        public void AddDataBaseConnectors_BlobStorageConnectorFactory_RegistersCorrectService()
        {
            // Arrange
            SetupAppSettingsWithBlobStorageDatabase();

            // Act
            _services.AddDataBaseConnectors();

            // Assert
            ServiceDescriptor? serviceDescriptor = _services.FirstOrDefault(sd =>
                sd.ServiceType == typeof(IDataBaseConnector) &&
                sd.ServiceKey?.ToString() == "TestBlobStorageDb");

            Assert.Multiple(() =>
            {
                Assert.That(serviceDescriptor, Is.Not.Null);
                Assert.That(serviceDescriptor!.Lifetime, Is.EqualTo(ServiceLifetime.Scoped));
                Assert.That(serviceDescriptor.ServiceKey, Is.EqualTo("TestBlobStorageDb"));
                Assert.That(serviceDescriptor.KeyedImplementationFactory, Is.Not.Null);
            });
        }

        [Test]
        public void AddDataBaseConnectors_MountedDatabaseMissingRootPath_ThrowsInvalidOperationException()
        {
            // Arrange
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                TestConfigFactory.MountedDb(0, "TestMountedDb", null) // omit root path
            );
            TestConfigFactory.BuildAndLoad(configData);
            _services.AddDataBaseConnectors();
            ServiceDescriptor serviceDescriptor = _services.First(sd =>
                sd.ServiceType == typeof(IDataBaseConnector) &&
                sd.ServiceKey?.ToString() == "TestMountedDb");

            // Act & Assert
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                serviceDescriptor.KeyedImplementationFactory!(_mockServiceProvider.Object, "TestMountedDb"));
            Assert.That(exception.Message, Is.EqualTo("Missing required custom configuration value 'RootPath' for database TestMountedDb"));
        }

        [Test]
        public void AddDataBaseConnectors_FileShareDatabaseMissingSharePath_ThrowsInvalidOperationException()
        {
            // Arrange
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                TestConfigFactory.FileShareDb(0, "TestFileShareDb", null) // omit share path
            );
            TestConfigFactory.BuildAndLoad(configData);
            _services.AddDataBaseConnectors();
            ServiceDescriptor serviceDescriptor = _services.First(sd =>
                sd.ServiceType == typeof(IDataBaseConnector) &&
                sd.ServiceKey?.ToString() == "TestFileShareDb");

            // Act & Assert
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                serviceDescriptor.KeyedImplementationFactory!(_mockServiceProvider.Object, "TestFileShareDb"));
            Assert.That(exception.Message, Is.EqualTo("Missing required custom configuration value 'SharePath' for database TestFileShareDb"));
        }

        [Test]
        public void AddDataBaseConnectors_BlobStorageDatabaseMissingConnectionString_ThrowsInvalidOperationException()
        {
            // Arrange
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                TestConfigFactory.BlobStorageDb(0, "TestBlobStorageDb", null, "test-container") // omit connection string
            );
            TestConfigFactory.BuildAndLoad(configData);
            _services.AddDataBaseConnectors();
            ServiceDescriptor serviceDescriptor = _services.First(sd =>
                sd.ServiceType == typeof(IDataBaseConnector) &&
                sd.ServiceKey?.ToString() == "TestBlobStorageDb");

            // Act & Assert
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                serviceDescriptor.KeyedImplementationFactory!(_mockServiceProvider.Object, "TestBlobStorageDb"));
            Assert.That(exception.Message, Is.EqualTo("Missing required custom configuration value 'ConnectionString' for database TestBlobStorageDb"));
        }

        [Test]
        public void AddDataBaseConnectors_BlobStorageDatabaseMissingContainerName_ThrowsInvalidOperationException()
        {
            // Arrange
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                TestConfigFactory.BlobStorageDb(0, "TestBlobStorageDb", "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=key;", null) // omit container name
            );
            TestConfigFactory.BuildAndLoad(configData);
            _services.AddDataBaseConnectors();
            ServiceDescriptor serviceDescriptor = _services.First(sd =>
                sd.ServiceType == typeof(IDataBaseConnector) &&
                sd.ServiceKey?.ToString() == "TestBlobStorageDb");

            // Act & Assert
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                serviceDescriptor.KeyedImplementationFactory!(_mockServiceProvider.Object, "TestBlobStorageDb"));
            Assert.That(exception.Message, Is.EqualTo("Missing required custom configuration value 'ContainerName' for database TestBlobStorageDb"));
        }

        [Test]
        public void AddDataBaseConnectors_MountedDatabaseEmptyRootPath_ThrowsInvalidOperationException()
        {
            // Arrange
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                TestConfigFactory.MountedDb(0, "TestMountedDb", string.Empty) // empty root path
            );
            TestConfigFactory.BuildAndLoad(configData);
            _services.AddDataBaseConnectors();
            ServiceDescriptor serviceDescriptor = _services.First(sd =>
                sd.ServiceType == typeof(IDataBaseConnector) &&
                sd.ServiceKey?.ToString() == "TestMountedDb");

            // Act & Assert
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                serviceDescriptor.KeyedImplementationFactory!(_mockServiceProvider.Object, "TestMountedDb"));
            Assert.That(exception.Message, Is.EqualTo("Missing required custom configuration value 'RootPath' for database TestMountedDb"));
        }

        [Test]
        public void AddDataBaseConnectors_WithUnsupportedDatabaseType_ThrowsInvalidOperationException()
        {
            // Arrange
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                new Dictionary<string, string?>
                {
                    ["DataBases:0:Type"] = "99",
                    ["DataBases:0:Id"] = "TestUnsupportedDb",
                    ["DataBases:0:CacheConfig:TableList:SlidingExpirationSeconds"] = "900",
                    ["DataBases:0:CacheConfig:TableList:AbsoluteExpirationSeconds"] = "900",
                    ["DataBases:0:CacheConfig:Meta:SlidingExpirationSeconds"] = "900",
                    ["DataBases:0:CacheConfig:Meta:AbsoluteExpirationSeconds"] = "900",
                    ["DataBases:0:CacheConfig:Groupings:SlidingExpirationSeconds"] = "900",
                    ["DataBases:0:CacheConfig:Groupings:AbsoluteExpirationSeconds"] = "900",
                    ["DataBases:0:CacheConfig:Data:SlidingExpirationSeconds"] = "600",
                    ["DataBases:0:CacheConfig:Data:AbsoluteExpirationSeconds"] = "600"
                }
            );
            TestConfigFactory.BuildAndLoad(configData);

            // Act & Assert
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                _services.AddDataBaseConnectors());
            Assert.That(exception.Message, Does.StartWith("Unsupported database type:"));
            Assert.That(exception.Message, Does.Contain("TestUnsupportedDb"));
        }

        [Test]
        public void AddDataBaseConnectors_WithEmptyDatabaseList_CompletesSuccessfully()
        {
            // Arrange
            Dictionary<string, string?> configData = TestConfigFactory.Base();
            TestConfigFactory.BuildAndLoad(configData);

            // Act
            _services.AddDataBaseConnectors();

            // Assert
            List<ServiceDescriptor> connectorServices = [.. _services.Where(sd => sd.ServiceType == typeof(IDataBaseConnector))];
            Assert.That(connectorServices, Is.Empty);
        }

        [Test]
        public void AddDataBaseConnectors_FileShareDatabaseEmptySharePath_ThrowsInvalidOperationException()
        {
            // Arrange
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                TestConfigFactory.FileShareDb(0, "TestFileShareDb", string.Empty)
            );
            TestConfigFactory.BuildAndLoad(configData);
            _services.AddDataBaseConnectors();
            ServiceDescriptor serviceDescriptor = _services.First(sd =>
                sd.ServiceType == typeof(IDataBaseConnector) &&
                sd.ServiceKey?.ToString() == "TestFileShareDb");

            // Act & Assert
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                serviceDescriptor.KeyedImplementationFactory!(_mockServiceProvider.Object, "TestFileShareDb"));
            Assert.That(exception.Message, Is.EqualTo("Missing required custom configuration value 'SharePath' for database TestFileShareDb"));
        }

        [Test]
        public void AddDataBaseConnectors_BlobStorageDatabaseEmptyConnectionString_ThrowsInvalidOperationException()
        {
            // Arrange
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                TestConfigFactory.BlobStorageDb(0, "TestBlobStorageDb", string.Empty, "test-container")
            );
            TestConfigFactory.BuildAndLoad(configData);
            _services.AddDataBaseConnectors();
            ServiceDescriptor serviceDescriptor = _services.First(sd =>
                sd.ServiceType == typeof(IDataBaseConnector) &&
                sd.ServiceKey?.ToString() == "TestBlobStorageDb");

            // Act & Assert
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                serviceDescriptor.KeyedImplementationFactory!(_mockServiceProvider.Object, "TestBlobStorageDb"));
            Assert.That(exception.Message, Is.EqualTo("Missing required custom configuration value 'ConnectionString' for database TestBlobStorageDb"));
        }

        [Test]
        public void AddDataBaseConnectors_BlobStorageDatabaseEmptyContainerName_ThrowsInvalidOperationException()
        {
            // Arrange
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                TestConfigFactory.BlobStorageDb(0, "TestBlobStorageDb", "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=key;", string.Empty)
            );
            TestConfigFactory.BuildAndLoad(configData);
            _services.AddDataBaseConnectors();
            ServiceDescriptor serviceDescriptor = _services.First(sd =>
                sd.ServiceType == typeof(IDataBaseConnector) &&
                sd.ServiceKey?.ToString() == "TestBlobStorageDb");

            // Act & Assert
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                serviceDescriptor.KeyedImplementationFactory!(_mockServiceProvider.Object, "TestBlobStorageDb"));
            Assert.That(exception.Message, Is.EqualTo("Missing required custom configuration value 'ContainerName' for database TestBlobStorageDb"));
        }

        private static void SetupAppSettingsWithMountedDatabase()
        {
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                TestConfigFactory.MountedDb(0, "TestMountedDb")
            );
            TestConfigFactory.BuildAndLoad(configData);
        }

        private static void SetupAppSettingsWithFileShareDatabase()
        {
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                TestConfigFactory.FileShareDb(0, "TestFileShareDb")
            );
            TestConfigFactory.BuildAndLoad(configData);
        }

        private static void SetupAppSettingsWithBlobStorageDatabase()
        {
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                TestConfigFactory.BlobStorageDb(0, "TestBlobStorageDb")
            );
            TestConfigFactory.BuildAndLoad(configData);
        }
    }
}