using Microsoft.Extensions.Configuration;
using PxApi.Configuration;

namespace PxApi.UnitTests.ConfigurationTests
{
    [TestFixture]
    public class QueryLimitsConfigTests
    {
        [Test]
        public void Constructor_WithBothLimitsSet_SetsPropertiesCorrectly()
        {
            // Arrange
            Dictionary<string, string?> configValues = new()
            {
                {"QueryLimits:JsonMaxCells", "50000"},
                {"QueryLimits:JsonStatMaxCells", "75000"}
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();
            IConfigurationSection section = configuration.GetSection("QueryLimits");

            // Act
            QueryLimitsConfig config = new(section);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(config.JsonMaxCells, Is.EqualTo(50000));
                Assert.That(config.JsonStatMaxCells, Is.EqualTo(75000));
            });
        }

        [Test]
        public void Constructor_WithNoLimitsSet_SetsPropertiesToZero()
        {
            // Arrange
            Dictionary<string, string?> configValues = [];
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();
            IConfigurationSection section = configuration.GetSection("QueryLimits");

            // Act
            QueryLimitsConfig config = new(section);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(config.JsonMaxCells, Is.EqualTo(long.MaxValue));
                Assert.That(config.JsonStatMaxCells, Is.EqualTo(long.MaxValue));
            });
        }

        [Test]
        public void Constructor_WithOnlyJsonMaxCellsSet_SetsOnlyJsonMaxCells()
        {
            // Arrange
            Dictionary<string, string?> configValues = new()
            {
                {"QueryLimits:JsonMaxCells", "25000"}
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();
            IConfigurationSection section = configuration.GetSection("QueryLimits");

            // Act
            QueryLimitsConfig config = new(section);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(config.JsonMaxCells, Is.EqualTo(25000));
                Assert.That(config.JsonStatMaxCells, Is.EqualTo(long.MaxValue));
            });
        }

        [Test]
        public void Constructor_WithOnlyJsonStatMaxCellsSet_SetsOnlyJsonStatMaxCells()
        {
            // Arrange
            Dictionary<string, string?> configValues = new()
            {
                {"QueryLimits:JsonStatMaxCells", "35000"}
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();
            IConfigurationSection section = configuration.GetSection("QueryLimits");

            // Act
            QueryLimitsConfig config = new(section);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(config.JsonMaxCells, Is.EqualTo(long.MaxValue));
                Assert.That(config.JsonStatMaxCells, Is.EqualTo(35000));
            });
        }

        [Test]
        public void Constructor_WithInvalidValues_SetsPropertiesToZero()
        {
            // Arrange
            Dictionary<string, string?> configValues = new()
            {
                {"QueryLimits:JsonMaxCells", "invalid"},
                {"QueryLimits:JsonStatMaxCells", "-100"}
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();
            IConfigurationSection section = configuration.GetSection("QueryLimits");

            // Act
            Assert.Throws<InvalidOperationException>(() => new QueryLimitsConfig(section));
        }
    }
}