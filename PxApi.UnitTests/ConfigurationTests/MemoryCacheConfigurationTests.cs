using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PxApi.Configuration;

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
            Dictionary<string, string?> configData = new()
            {
                ["RootUrl"] = "https://testurl.fi",
                ["Cache:MaxSizeBytes"] = expectedCacheSize.ToString(),
                ["Cache:DefaultDataCellSize"] = "32",
                ["Cache:DefaultUpdateTaskSize"] = "100",
                ["Cache:DefaultTableGroupSize"] = "200",
                ["Cache:DefaultFileListSize"] = "500000",
                ["Cache:DefaultMetaSize"] = "300000",
                ["DataBases:0:Type"] = "Mounted",
                ["DataBases:0:Id"] = "TestDb",
                ["DataBases:0:CacheConfig:TableList:SlidingExpirationSeconds"] = "900",
                ["DataBases:0:CacheConfig:TableList:AbsoluteExpirationSeconds"] = "900",
                ["DataBases:0:CacheConfig:Meta:SlidingExpirationSeconds"] = "900",
                ["DataBases:0:CacheConfig:Meta:AbsoluteExpirationSeconds"] = "900",
                ["DataBases:0:CacheConfig:Groupings:SlidingExpirationSeconds"] = "900",
                ["DataBases:0:CacheConfig:Groupings:AbsoluteExpirationSeconds"] = "900",
                ["DataBases:0:CacheConfig:Data:SlidingExpirationSeconds"] = "600",
                ["DataBases:0:CacheConfig:Data:AbsoluteExpirationSeconds"] = "600",
                ["DataBases:0:Custom:RootPath"] = "datasource/root/"
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            AppSettings.Load(configuration);

            ServiceCollection services = new();
            
            // Configure MemoryCache the same way as in Program.cs
            services.AddMemoryCache(options =>
            {
                options.SizeLimit = AppSettings.Active.Cache.MaxSizeBytes;
            });

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
            Dictionary<string, string?> configData = new()
            {
                ["RootUrl"] = "https://testurl.fi",
                ["DataBases:0:Type"] = "Mounted",
                ["DataBases:0:Id"] = "TestDb",
                ["DataBases:0:CacheConfig:TableList:SlidingExpirationSeconds"] = "900",
                ["DataBases:0:CacheConfig:TableList:AbsoluteExpirationSeconds"] = "900",
                ["DataBases:0:CacheConfig:Meta:SlidingExpirationSeconds"] = "900",
                ["DataBases:0:CacheConfig:Meta:AbsoluteExpirationSeconds"] = "900",
                ["DataBases:0:CacheConfig:Groupings:SlidingExpirationSeconds"] = "900",
                ["DataBases:0:CacheConfig:Groupings:AbsoluteExpirationSeconds"] = "900",
                ["DataBases:0:CacheConfig:Data:SlidingExpirationSeconds"] = "600",
                ["DataBases:0:CacheConfig:Data:AbsoluteExpirationSeconds"] = "600",
                ["DataBases:0:Custom:RootPath"] = "datasource/root/"
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            AppSettings.Load(configuration);

            ServiceCollection services = new();
            
            // Configure MemoryCache the same way as in Program.cs
            services.AddMemoryCache(options =>
            {
                options.SizeLimit = AppSettings.Active.Cache.MaxSizeBytes;
            });

            ServiceProvider serviceProvider = services.BuildServiceProvider();

            // Act
            IOptions<MemoryCacheOptions> memoryCacheOptions = serviceProvider.GetRequiredService<IOptions<MemoryCacheOptions>>();

            // Assert
            Assert.That(memoryCacheOptions.Value.SizeLimit, Is.EqualTo(524288000)); // 500 MB default
        }
    }
}