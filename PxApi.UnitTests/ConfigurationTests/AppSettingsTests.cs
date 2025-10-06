using Microsoft.Extensions.Configuration;
using PxApi.Configuration;

namespace PxApi.UnitTests.ConfigurationTests
{
    [TestFixture]
    public class AppSettingsTests
    {
        [Test]
        public void AppSettings_WhenCacheConfigurationProvided_ShouldLoadCacheSettings()
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

            // Act
            AppSettings.Load(configuration);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(AppSettings.Active.Cache.MaxSizeBytes, Is.EqualTo(expectedCacheSize));
                Assert.That(AppSettings.Active.Cache.DefaultDataCellSize, Is.EqualTo(32));
                Assert.That(AppSettings.Active.Cache.DefaultUpdateTaskSize, Is.EqualTo(100));
                Assert.That(AppSettings.Active.Cache.DefaultTableGroupSize, Is.EqualTo(200));
                Assert.That(AppSettings.Active.Cache.DefaultFileListSize, Is.EqualTo(500000));
                Assert.That(AppSettings.Active.Cache.DefaultMetaSize, Is.EqualTo(300000));
            });
        }

        [Test]
        public void AppSettings_WhenCacheConfigurationNotProvided_ShouldUseDefaultCacheSize()
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

            // Act
            AppSettings.Load(configuration);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(AppSettings.Active.Cache.MaxSizeBytes, Is.EqualTo(524288000)); // 512 MB default
                Assert.That(AppSettings.Active.Cache.DefaultDataCellSize, Is.EqualTo(16));
                Assert.That(AppSettings.Active.Cache.DefaultUpdateTaskSize, Is.EqualTo(50));
                Assert.That(AppSettings.Active.Cache.DefaultTableGroupSize, Is.EqualTo(100));
                Assert.That(AppSettings.Active.Cache.DefaultFileListSize, Is.EqualTo(350000));
                Assert.That(AppSettings.Active.Cache.DefaultMetaSize, Is.EqualTo(200000));
            });
        }
    }
}