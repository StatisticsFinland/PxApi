using Microsoft.Extensions.Configuration;
using PxApi.Configuration;
using PxApi.OpenApi.Examples;
using PxApi.UnitTests.Utils;
using System.Text.Json.Nodes;

namespace PxApi.UnitTests.OpenApi.Examples
{
    [TestFixture]
    public class DatabaseListingExampleTests
    {
        private const string TestDatabaseId = "StatFin";

        [SetUp]
        public void Setup()
        {
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                TestConfigFactory.MountedDb(0, TestDatabaseId, "datasource/root/")
            );
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();
            AppSettings.Load(configuration);
        }

        [Test]
        public void Instance_ShouldReturnJsonArray()
        {
            // Act
            JsonNode instance = DatabaseListingExample.Instance;

            // Assert
            Assert.That(instance, Is.InstanceOf<JsonArray>());
        }

        [Test]
        public void Instance_ShouldContainSingleDatabase()
        {
            // Act
            JsonNode instance = DatabaseListingExample.Instance;
            JsonArray array = (JsonArray)instance;

            // Assert
            Assert.That(array, Has.Count.EqualTo(1));
        }

        [Test]
        public void Instance_ShouldHaveCorrectDatabaseProperties()
        {
            // Act
            JsonNode instance = DatabaseListingExample.Instance;
            JsonArray array = (JsonArray)instance;
            JsonObject database = (JsonObject)array[0]!;

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(database["id"]?.ToString(), Is.EqualTo(TestDatabaseId));
                Assert.That(database["name"]?.ToString(), Is.EqualTo(TestDatabaseId));
                Assert.That(database["description"], Is.Null);
                Assert.That(database["tableCount"]?.GetValue<int>(), Is.EqualTo(1526));
            }
        }

        [Test]
        public void Instance_ShouldHaveCorrectAvailableLanguages()
        {
            // Act
            JsonNode instance = DatabaseListingExample.Instance;
            JsonArray array = (JsonArray)instance;
            JsonObject database = (JsonObject)array[0]!;
            JsonArray? languages = database["availableLanguages"] as JsonArray;

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(languages, Is.Not.Null);
                Assert.That(languages!, Has.Count.EqualTo(3));
                Assert.That(languages![0]?.ToString(), Is.EqualTo("fi"));
                Assert.That(languages![1]?.ToString(), Is.EqualTo("sv"));
                Assert.That(languages![2]?.ToString(), Is.EqualTo("en"));
            }
        }

        [Test]
        public void Instance_ShouldHaveLinksArray()
        {
            // Act
            JsonNode instance = DatabaseListingExample.Instance;
            JsonArray array = (JsonArray)instance;
            JsonObject database = (JsonObject)array[0]!;
            JsonArray? links = database["links"] as JsonArray;

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(links, Is.Not.Null);
                Assert.That(links!, Has.Count.EqualTo(1));
            }
        }

        [Test]
        public void Instance_ShouldHaveCorrectLinkProperties()
        {
            // Act
            JsonNode instance = DatabaseListingExample.Instance;
            JsonArray array = (JsonArray)instance;
            JsonObject database = (JsonObject)array[0]!;
            JsonArray? links = database["links"] as JsonArray;
            JsonObject link = (JsonObject)links![0]!;

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(link["href"]?.ToString(), Does.Contain("https://testurl.fi"));
                Assert.That(link["href"]?.ToString(), Does.Contain($"meta/databases/{TestDatabaseId}/tables"));
                Assert.That(link["href"]?.ToString(), Does.Contain("lang=fi"));
                Assert.That(link["rel"]?.ToString(), Is.EqualTo("describedby"));
                Assert.That(link["method"]?.ToString(), Is.EqualTo("GET"));
            }
        }

        [Test]
        public void Instance_ShouldUseDynamicRootUrlFromConfiguration()
        {
            // Arrange
            const string customRootUrl = "https://custom.example.com/api";
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                TestConfigFactory.MountedDb(0, TestDatabaseId, "datasource/root/"),
                new Dictionary<string, string?>
                {
                    ["RootUrl"] = customRootUrl
                }
            );
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();
            AppSettings.Load(configuration);

            // Act
            JsonNode instance = DatabaseListingExample.Instance;
            JsonArray array = (JsonArray)instance;
            JsonObject database = (JsonObject)array[0]!;
            JsonArray? links = database["links"] as JsonArray;
            JsonObject link = (JsonObject)links![0]!;

            // Assert
            Assert.That(link["href"]?.ToString(), Does.StartWith(customRootUrl));
        }
    }
}
