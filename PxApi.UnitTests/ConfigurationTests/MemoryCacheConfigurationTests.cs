using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PxApi.Configuration;
using PxApi.UnitTests.Utils;

namespace PxApi.UnitTests.ConfigurationTests
{
    [TestFixture]
    public class MemoryCacheConfigurationTests
    {
        [Test]
        public void MemoryCache_WhenConfiguredWithSizeLimit_ShouldHaveCorrectSizeLimit()
        {
            // Arrange
            const long expectedCacheSize = 134217728; // 128 MB
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                TestConfigFactory.MountedDb(0, "TestDb", "datasource/root/"),
                new Dictionary<string, string?>
                {
                    ["Cache:MaxSizeBytes"] = expectedCacheSize.ToString(),
                    ["Cache:DefaultDataCellSize"] = "32",
                    ["Cache:DefaultUpdateTaskSize"] = "100",
                    ["Cache:DefaultTableGroupSize"] = "200",
                    ["Cache:DefaultFileListSize"] = "500000",
                    ["Cache:DefaultMetaSize"] = "300000"
                }
            );
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            AppSettings.Load(configuration);
            ServiceCollection services = new();
            services.AddMemoryCache(options => { options.SizeLimit = AppSettings.Active.Cache.MaxSizeBytes; });
            ServiceProvider serviceProvider = services.BuildServiceProvider();

            // Act
            IOptions<MemoryCacheOptions> memoryCacheOptions = serviceProvider.GetRequiredService<IOptions<MemoryCacheOptions>>();

            // Assert
            Assert.That(memoryCacheOptions.Value.SizeLimit, Is.EqualTo(expectedCacheSize));
        }

        [Test]
        public void MemoryCache_WhenDefaultConfiguration_ShouldHaveDefaultSizeLimit()
        {
            // Arrange
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                TestConfigFactory.MountedDb(0, "TestDb", "datasource/root/")
            );
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            AppSettings.Load(configuration);
            ServiceCollection services = new();
            services.AddMemoryCache(options => { options.SizeLimit = AppSettings.Active.Cache.MaxSizeBytes; });
            ServiceProvider serviceProvider = services.BuildServiceProvider();

            // Act
            IOptions<MemoryCacheOptions> memoryCacheOptions = serviceProvider.GetRequiredService<IOptions<MemoryCacheOptions>>();

            // Assert
            Assert.That(memoryCacheOptions.Value.SizeLimit, Is.EqualTo(524288000)); // 500 MB default
        }
    }
}