using Microsoft.Extensions.Configuration;
using PxApi.Configuration;

namespace PxApi.UnitTests.ConfigurationTests
{
    [TestFixture]
    public class CorsConfigTests
    {
        [Test]
        public void Constructor_WithAllowedOrigins_SetsOriginsCorrectly()
        {
            // Arrange
            Dictionary<string, string?> configValues = new()
            {
                {"Cors:AllowedOrigins:0", "https://example.com"},
                {"Cors:AllowedOrigins:1", "https://other.com"}
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();
            IConfigurationSection section = configuration.GetSection("Cors");

            // Act
            CorsConfig config = new(section);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(config.HasAllowedOrigins, Is.True);
                Assert.That(config.AllowedOrigins, Has.Count.EqualTo(2));
                Assert.That(config.AllowedOrigins[0], Is.EqualTo("https://example.com"));
                Assert.That(config.AllowedOrigins[1], Is.EqualTo("https://other.com"));
            }
        }

        [Test]
        public void Constructor_WithEmptyAllowedOrigins_HasNoOrigins()
        {
            // Arrange
            Dictionary<string, string?> configValues = [];
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();
            IConfigurationSection section = configuration.GetSection("Cors");

            // Act
            CorsConfig config = new(section);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(config.HasAllowedOrigins, Is.False);
                Assert.That(config.AllowedOrigins, Is.Empty);
            }
        }

        [Test]
        public void Constructor_WithSingleOrigin_SetsSingleOrigin()
        {
            // Arrange
            Dictionary<string, string?> configValues = new()
            {
                {"Cors:AllowedOrigins:0", "https://example.com"}
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();
            IConfigurationSection section = configuration.GetSection("Cors");

            // Act
            CorsConfig config = new(section);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(config.HasAllowedOrigins, Is.True);
                Assert.That(config.AllowedOrigins, Has.Count.EqualTo(1));
                Assert.That(config.AllowedOrigins[0], Is.EqualTo("https://example.com"));
            }
        }
    }
}
